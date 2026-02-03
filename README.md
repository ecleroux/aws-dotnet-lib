# AWS .NET Core

A lightweight, well-documented .NET 8.0 library for AWS S3 and SQS operations with support for multiple authentication methods including assume role, web identity tokens, and basic credentials.

## Features

### S3 Operations (`IAWSS3Client`)
- **File Operations**: Upload, download, and delete files and directories
- **Object Management**: List, copy, and verify S3 objects
- **Authentication**: Basic credentials, assume role, and web identity token support
- **Checksums**: SHA256 checksum computation for data integrity verification
- **Connection Testing**: Built-in S3 bucket connectivity validation

### SQS Operations (`IAWSSQSService`)
- **Message Operations**: Send, receive, and delete messages from queues
- **Visibility Management**: Change message visibility timeouts
- **Credential Caching**: Thread-safe cached credentials with automatic expiration
- **Token Refresh**: Automatic token refresh on expiration with retry logic
- **Web Identity**: Native support for OIDC web identity token authentication

## Requirements

- .NET 8.0 or later
- AWS SDK for .NET (NuGet packages):
  - `AWSSDK.S3`
  - `AWSSDK.SecurityToken`
  - `AWSSDK.SQS`

## Installation

1. Clone the repository:
```bash
git clone <repository-url>
cd aws-dotnet-core
```

2. Build the project:
```bash
dotnet build
```

3. Reference in your project:
```xml
<ProjectReference Include="path/to/AWS.Core/AWS.Core.csproj" />
```

## Usage

### S3 Operations

#### Basic Credentials
```csharp
var s3Client = new AWSS3Client();

// Create storage context with basic credentials
var context = s3Client.GetStorageContextWithBasicAWSCredentials(
    accessKey: "YOUR_ACCESS_KEY",
    secretKey: "YOUR_SECRET_KEY"
);

// Upload a file
await s3Client.UploadFileAsync(
    storageContext: context,
    bucketName: "my-bucket",
    region: "us-east-1",
    objectKey: "path/to/file.txt",
    localFilepath: "/local/path/file.txt"
);
```

#### Assume Role with Web Identity Token
```csharp
var s3Client = new AWSS3Client();

var context = await s3Client.GetStorageContextWithAssumeRoleWithWebIdentityAsync(
    tokenFilepath: "/path/to/token",
    region: "us-east-1",
    role: "arn:aws:iam::123456789012:role/MyRole",
    roleSessionName: "my-session"
);

// Download a file
await s3Client.DownloadFileAsync(
    storageContext: context,
    bucketName: "my-bucket",
    region: "us-east-1",
    objectKey: "path/to/file.txt",
    localFilepath: "/local/destination/file.txt"
);
```

#### Assume Role
```csharp
var s3Client = new AWSS3Client();

var basicCredentials = new BasicAWSCredentials("accessKey", "secretKey");
var assumedCredentials = await s3Client.GetAWSCredentialsWithAssumeRoleAsync(
    credentials: basicCredentials,
    role: "arn:aws:iam::123456789012:role/MyRole",
    region: "us-east-1",
    roleSessionName: "my-session"
);
```

#### Additional S3 Operations
```csharp
// List objects
var objects = await s3Client.ListS3ObjectsAsync(context, "my-bucket", "us-east-1", "prefix/");

// Copy objects
await s3Client.CopyObjectsAsync(
    storageContext: context,
    region: "us-east-1",
    sourceBucketName: "source-bucket",
    sourceKeyPrefix: "source/",
    destinationBucketName: "dest-bucket",
    destinationKeyPrefix: "dest/",
    deleteSource: false
);

// Delete objects
await s3Client.DeleteObjectsAsync(context, "my-bucket", "us-east-1", "prefix/");

// Get object information (checksums and sizes)
var (checksum, sizeInBytes, keys) = await s3Client.GetObjectsInfoAsync(
    context, "my-bucket", "us-east-1", "prefix/"
);

// Test connectivity
var testResults = await s3Client.TestConnectionAsync(context, "my-bucket", "us-east-1");
foreach (var result in testResults)
    Console.WriteLine(result);
```

### SQS Operations

```csharp
var sqsService = new AWSSQSService();

var sqsContext = new AWSSQSContext
{
    Region = "us-east-1",
    RuntimeEnvironmentCode = "Dev",
    AccountId = "123456789012",
    CommandQueueName = "my-queue"
};

// Initialize the SQS client
await sqsService.SetAmazonSQSClientAsync(sqsContext);

// Send a message
await sqsService.SendMessageAsync(sqsContext, "Hello, Queue!");

// Receive messages
var receiveRequest = new ReceiveMessageRequest
{
    QueueUrl = sqsContext.CommandQueueURL,
    MaxNumberOfMessages = 1,
    WaitTimeSeconds = 20
};

var response = await sqsService.ReceiveMessageAsync(sqsContext, receiveRequest);

foreach (var message in response.Messages)
{
    Console.WriteLine($"Message: {message.Body}");
    
    // Delete the message after processing
    await sqsService.DeleteMessageAsync(sqsContext, message);
}

// Change message visibility
var visibilityRequest = new ChangeMessageVisibilityRequest
{
    QueueUrl = sqsContext.CommandQueueURL,
    ReceiptHandle = message.ReceiptHandle,
    VisibilityTimeout = 60
};

await sqsService.ChangeMessageVisibilityAsync(sqsContext, visibilityRequest);
```

## Architecture

### Models

#### AWSStorageContext
Lightweight container for S3 operations. Holds the S3 client and credentials for reuse across multiple operations.

```csharp
public class AWSStorageContext
{
    public AmazonS3Client? S3Client { get; set; }
    public AWSCredentials? Credentials { get; set; }
}
```

**Usage:** Create once, reuse for multiple S3 operations. The S3 client is lazily initialized on first use by `InitializeS3Client()`.

#### AWSSQSContext
Configuration context for SQS operations. Uses `required` properties to ensure all necessary configuration is provided before use.

```csharp
public class AWSSQSContext
{
    public IAmazonSQS AmazonSQS { get; set; } = null!;
    public required string Region { get; set; }
    public required string RuntimeEnvironmentCode { get; set; }
    public required string AccountId { get; set; }
    public required string CommandQueueName { get; set; }
    public string CommandQueueURL => $"https://sqs.{Region}.amazonaws.com/{AccountId}/{CommandQueueName}";
}
```

**Key Points:**
- All properties except `AmazonSQS` are `required` - ensures complete configuration
- `CommandQueueURL` is computed dynamically from region, account ID, and queue name
- `AmazonSQS` is initialized via `SetAmazonSQSClientAsync()` before use

### Services
- `AWSS3Client`: Implements `IAWSS3Client` interface for S3 operations
- `AWSSQSService`: Implements `IAWSSQSService` interface for SQS operations

### Authentication
- Basic AWS credentials
- Assume role (STS)
- Web identity token (OIDC)
- Automatic token refresh with retry logic

## Error Handling

The library uses specific exception types:
- `ArgumentNullException`: For null or missing required parameters
- `ArgumentException`: For invalid argument values
- `InvalidOperationException`: For S3/SQS operation failures
- `AmazonServiceException`: For AWS service errors

Expired token errors are automatically retried with refreshed credentials.

## Configuration

The library uses environment-based configuration. Update the following in your application:
- Token file path (default: `/etc/podinfo/token`)
- Role ARN
- Session names
- AWS regions and endpoints

Refer to TODO comments in the source code for configuration points.

## Testing

Run the included S3 connection test:
```csharp
var testResults = await s3Client.TestConnectionAsync(context, "my-bucket", "us-east-1");
```

This validates:
- ListBucket permissions
- PutObject permissions
- GetObject permissions
- DeleteObject permissions
- TransferUtility operations

## Notes

- All TODO items in the code should be configured for your specific AWS environment.
- The library uses .NET 8.0 features including records and nullable reference types.
- Thread-safe credential caching is implemented using `ConcurrentDictionary` with semaphore-based double-checked locking.

## License

This project is licensed under the MIT License - see below for details.

```
MIT License

Copyright (c) 2026

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

You are free to use this project for any purpose - commercial, personal, or educational.
