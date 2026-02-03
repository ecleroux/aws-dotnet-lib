using System.Collections.Concurrent;
using Amazon;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using AWS.Core.Models;
using AWS.Core.Interfaces;

namespace AWS.Core.Services;

/// <summary>
/// AWS SQS service implementation providing client initialization and message operations
/// </summary>
/// <remarks>
/// Manages AWS SQS client creation using Web Identity Token authentication, handles credential
/// caching with automatic expiration, and provides methods for message operations with token
/// refresh capability. Uses double-checked locking for thread-safe credential initialization.
/// </remarks>
public class AWSSQSService : IAWSSQSService
{
    private static readonly ConcurrentDictionary<string, (AmazonSQSClient Client, DateTime Expiry)> _sessionCache = new ConcurrentDictionary<string, (AmazonSQSClient, DateTime)>();
    private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    public AWSSQSService()
    {
    }

    /// <summary>Initializes the SQS client with AWS credentials obtained via Web Identity Token</summary>
    /// <param name="awsSQSContext">Context object to populate with initialized SQS client</param>
    /// <remarks>
    /// Uses cached credentials when available and not expired. Implements double-checked locking
    /// for thread-safe credential initialization. Automatically retries on expired token errors.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when credential initialization fails</exception>
    public async Task SetAmazonSQSClientAsync(AWSSQSContext awsSQSContext)
    {
        string roleSessionName = $"your_role_session_name"; //TODO: This should be set appropriately, possibly from configuration or context
        
        // First check (outside the lock)
        if (_sessionCache.TryGetValue(roleSessionName, out var cachedSession) && cachedSession.Expiry > DateTime.UtcNow)
        {
            awsSQSContext.AmazonSQS = cachedSession.Client;
            return;
        }

        await _semaphore.WaitAsync();
        try
        {
            // Second check (inside the lock)
            if (_sessionCache.TryGetValue(roleSessionName, out cachedSession) && cachedSession.Expiry > DateTime.UtcNow)
            {
                awsSQSContext.AmazonSQS = cachedSession.Client;
                return;
            }

            try
            {
                //Use WebIdentityToken to get credentials
                string tokenFilePath = "/etc/podinfo/token"; // TODO: Path to the token file
                string role = "your_role_arn"; //TODO: This should be set appropriately, possibly from configuration or context

                using (var securityTokenServiceClient = new AmazonSecurityTokenServiceClient(RegionEndpoint.GetBySystemName(awsSQSContext.Region)))
                {
                    //Note: This allows the code to be used in parallel without role session name conflicts. Example if you are listening to multiple queues in parallel. If not needed, you can simplify this.
                    var roleSessionNameWithGuid = $"{roleSessionName}_{Guid.NewGuid()}";
                    if (roleSessionNameWithGuid.Length > 64)
                        roleSessionNameWithGuid = roleSessionNameWithGuid.Substring(0, 64);

                    var request = new AssumeRoleWithWebIdentityRequest()
                    {
                        WebIdentityToken = File.ReadAllText(tokenFilePath),
                        RoleArn = role,
                        RoleSessionName = roleSessionNameWithGuid,
                        DurationSeconds = 3600
                    };
        
                    var assumeRoleResult = await securityTokenServiceClient.AssumeRoleWithWebIdentityAsync(request);

                    var newClient = new AmazonSQSClient(assumeRoleResult.Credentials, RegionEndpoint.GetBySystemName(awsSQSContext.Region));
                    
                    if (assumeRoleResult.Credentials.Expiration == null)
                        throw new InvalidOperationException("Credential expiration time is null");
                    
                    var expiry = assumeRoleResult.Credentials.Expiration.Value.ToUniversalTime();

                    _sessionCache[roleSessionName] = (newClient, expiry);
                    awsSQSContext.AmazonSQS = newClient;
                }
            }
            catch (AmazonServiceException exception)
            {
                if (exception.ErrorCode == "ExpiredToken")
                    await SetAmazonSQSClientAsync(awsSQSContext);
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to initialize AWS SQS client credentials", ex);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>Executes an SQS operation with automatic token refresh on expiration</summary>
    /// <typeparam name="T">Return type of the operation</typeparam>
    /// <param name="awsSQSContext">AWS SQS context containing client and queue configuration</param>
    /// <param name="operation">Async operation to execute</param>
    /// <returns>Result of the operation</returns>
    /// <remarks>
    /// Automatically handles expired token scenarios by refreshing credentials and retrying.
    /// Other AWS service exceptions are re-thrown without retry.
    /// </remarks>
    private async Task<T> ExecuteWithTokenRefreshAsync<T>(AWSSQSContext awsSQSContext, Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (AmazonServiceException exception) when (exception.ErrorCode == "ExpiredToken")
        {
            await SetAmazonSQSClientAsync(awsSQSContext);
            return await operation();
        }
    }

    /// <summary>Changes the visibility timeout of a message in the SQS queue</summary>
    /// <param name="awsSQSContext">AWS SQS context containing client and queue configuration</param>
    /// <param name="changeMessageVisibilityRequest">Request containing message handle and new visibility timeout</param>
    /// <returns>Response from AWS SQS service</returns>
    public async Task<ChangeMessageVisibilityResponse> ChangeMessageVisibilityAsync(AWSSQSContext awsSQSContext, ChangeMessageVisibilityRequest changeMessageVisibilityRequest)
    {
        return await ExecuteWithTokenRefreshAsync(awsSQSContext, () => awsSQSContext.AmazonSQS.ChangeMessageVisibilityAsync(changeMessageVisibilityRequest));
    }

    /// <summary>Deletes a message from the SQS queue</summary>
    /// <param name="awsSQSContext">AWS SQS context containing client and queue configuration</param>
    /// <param name="message">Message to delete from the queue</param>
    /// <returns>Response from AWS SQS service</returns>
    public async Task<DeleteMessageResponse> DeleteMessageAsync(AWSSQSContext awsSQSContext, Message message)
    {
        return await ExecuteWithTokenRefreshAsync(awsSQSContext, () => awsSQSContext.AmazonSQS.DeleteMessageAsync(awsSQSContext.CommandQueueURL, message.ReceiptHandle));
    }

    /// <summary>Receives messages from the SQS queue</summary>
    /// <param name="awsSQSContext">AWS SQS context containing client and queue configuration</param>
    /// <param name="receiveMessageRequest">Request containing queue URL, max messages, and wait time</param>
    /// <returns>Response containing messages received from the queue</returns>
    public async Task<ReceiveMessageResponse> ReceiveMessageAsync(AWSSQSContext awsSQSContext, ReceiveMessageRequest receiveMessageRequest)
    {
        return await ExecuteWithTokenRefreshAsync(awsSQSContext, () => awsSQSContext.AmazonSQS.ReceiveMessageAsync(receiveMessageRequest));
    }

    /// <summary>Sends a message to the SQS queue</summary>
    /// <param name="awsSQSContext">AWS SQS context containing client and queue configuration</param>
    /// <param name="message">Message body to send to the queue</param>
    public async Task SendMessageAsync(AWSSQSContext awsSQSContext, string message)
    {
        await ExecuteWithTokenRefreshAsync(awsSQSContext, () => awsSQSContext.AmazonSQS.SendMessageAsync(awsSQSContext.CommandQueueURL, message));
    }
}