using Amazon.Runtime;
using Amazon.S3;

namespace AWS.Core.Models;
public class AWSStorageContext
{
    public AmazonS3Client? S3Client { get; set; }
    public AWSCredentials? Credentials { get; set; }
}