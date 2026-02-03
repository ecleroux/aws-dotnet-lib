using Amazon.SQS;

namespace AWS.Core.Models;

/// <summary>
/// Configuration and authentication context for AWS SQS operations
/// </summary>
/// <remarks>
/// Contains the SQS client instance, AWS region, and queue URL needed to perform
/// message operations against an AWS SQS queue. This is operational context passed
/// to SQS service methods.
/// </remarks>
public class AWSSQSContext
{
    /// <summary>AWS SQS client instance for making API calls</summary>
    public IAmazonSQS AmazonSQS { get; set; } = null!;
    
    /// <summary>AWS region where the SQS queue is located</summary>
    public required string Region { get; set; }
    
    /// <summary>Runtime environment code (e.g., Dev, Test, Prod) for context-specific operations</summary>
    public required string RuntimeEnvironmentCode { get; set; }

    /// <summary>  AWS Account ID owning the SQS queue </summary>
    public required string AccountId { get; set; }

    /// <summary>Name of the SQS queue to operate on</summary>
    public required string CommandQueueName { get; set; }

    /// <summary>URL of the SQS queue to operate on</summary>
    public string CommandQueueURL { get {return $"https://sqs.{Region}.amazonaws.com/{AccountId}/{CommandQueueName}"; } }
}