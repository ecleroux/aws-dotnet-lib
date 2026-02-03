using System.Threading.Tasks;
using Amazon.SQS.Model;
using AWS.Core.Models;

namespace AWS.Core.Interfaces;

/// <summary>
/// Service for managing AWS Simple Queue Service (SQS) operations.
/// Provides methods for sending, receiving, and managing messages in SQS queues.
/// </summary>
/// <remarks>
/// This service acts as a wrapper around the AWS SQS client, providing simplified interfaces
/// for common SQS operations. Each method requires an AWSSQSContext to supply authentication
/// and queue context information. The service supports standard SQS patterns including:
/// - Message sending to queues
/// - Long-polling message reception with configurable wait times
/// - Message visibility timeout management for processing guarantees
/// - Message deletion after successful processing
/// </remarks>
public interface IAWSSQSService
{
    /// <summary>
    /// Sets or reinitializes the underlying Amazon SQS client with new credentials
    /// </summary>
    /// <remarks>
    /// This method allows the service to update its SQS client configuration at runtime,
    /// which is useful for credential rotation or switching between different AWS accounts/regions.
    /// Should typically be called once during service initialization or when credentials expire.
    /// </remarks>
    /// <param name="awsSQSContext">Context containing AWS credentials and queue configuration details</param>
    Task SetAmazonSQSClientAsync(AWSSQSContext awsSQSContext);
    
    /// <summary>
    /// Changes the message visibility timeout for a message being processed from the queue
    /// </summary>
    /// <remarks>
    /// SQS messages have a visibility timeout that prevents other consumers from receiving the same message.
    /// Use this method to extend the timeout if processing takes longer than initially expected,
    /// or to reduce it if processing completes early. Visibility timeout is measured in seconds.
    /// </remarks>
    /// <param name="awsSQSContext">Context containing AWS credentials and queue configuration</param>
    /// <param name="changeMessageVisibilityRequest">Request containing the message receipt handle and new visibility timeout in seconds</param>
    /// <returns>Response from AWS indicating successful visibility timeout change</returns>
    Task<ChangeMessageVisibilityResponse> ChangeMessageVisibilityAsync(AWSSQSContext awsSQSContext, ChangeMessageVisibilityRequest changeMessageVisibilityRequest);
    
    /// <summary>
    /// Deletes a message from the queue after it has been successfully processed
    /// </summary>
    /// <remarks>
    /// Once a message is successfully processed, it must be explicitly deleted from the queue.
    /// If a message is not deleted within its visibility timeout, it will become visible again
    /// and may be reprocessed by other consumers. Always delete messages after confirmation
    /// of successful processing to maintain queue integrity.
    /// </remarks>
    /// <param name="awsSQSContext">Context containing AWS credentials and queue configuration</param>
    /// <param name="message">The SQS message object to delete (must include the receipt handle)</param>
    /// <returns>Response from AWS confirming message deletion</returns>
    Task<DeleteMessageResponse> DeleteMessageAsync(AWSSQSContext awsSQSContext, Message message);
    
    /// <summary>
    /// Receives messages from the SQS queue using long-polling
    /// </summary>
    /// <remarks>
    /// This method implements long-polling to efficiently wait for messages without constant API calls.
    /// The wait time is specified in the ReceiveMessageRequest (typically 0-20 seconds for long-polling).
    /// Messages returned have a visibility timeout during which only the receiving consumer can process them.
    /// The request can specify message attributes to retrieve and maximum number of messages (1-10).
    /// </remarks>
    /// <param name="awsSQSContext">Context containing AWS credentials and queue configuration</param>
    /// <param name="receiveMessageRequest">Request specifying poll wait time, max messages, and attributes to retrieve</param>
    /// <returns>Response containing the received messages from the queue (may be empty if no messages available)</returns>
    Task<ReceiveMessageResponse> ReceiveMessageAsync(AWSSQSContext awsSQSContext, ReceiveMessageRequest receiveMessageRequest);
    
    /// <summary>
    /// Sends a message to the SQS queue
    /// </summary>
    /// <remarks>
    /// Messages are stored in the queue until they are received and deleted by a consumer.
    /// The message content is the body string and can be any text data (JSON, plain text, etc.).
    /// SQS guarantees at-least-once delivery but not exactly-once, so consumers should handle
    /// potential duplicate messages by implementing idempotent processing logic.
    /// </remarks>
    /// <param name="awsSQSContext">Context containing AWS credentials and queue configuration</param>
    /// <param name="message">The message body to send to the queue as a string</param>
    /// <returns>A task representing the asynchronous send operation</returns>
    Task SendMessageAsync(AWSSQSContext awsSQSContext, string message);
}