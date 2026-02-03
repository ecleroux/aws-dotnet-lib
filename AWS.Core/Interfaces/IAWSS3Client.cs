using Amazon.Runtime;
using Amazon.S3.Model;
using AWS.Core.Models;

namespace AWS.Core.Interfaces;

public interface IAWSS3Client
{
    /// <summary>
    /// Gets an AWSStorageContext using basic AWS credentials.
    /// </summary>
    /// <param name="accessKey">AWS Access Key</param>
    /// <param name="secretKey">AWS Secret Key</param>
    /// <returns>AWSStorageContext containing the AWS credentials</returns>
    AWSStorageContext GetStorageContextWithBasicAWSCredentials(string accessKey, string secretKey);

    /// <summary>
    /// Gets an AWSStorageContext using Assume Role with Web Identity.
    /// </summary>
    /// <param name="tokenFilepath">Path to the web identity token file</param>
    /// <param name="region">AWS Region</param>
    /// <param name="role">AWS Role ARN to assume</param>
    /// <param name="roleSessionName">Role session name for the assumed role</param>
    /// <returns>AWSStorageContext containing the AWS credentials</returns>
    Task <AWSStorageContext> GetStorageContextWithAssumeRoleWithWebIdentityAsync(string tokenFilepath, string region, string role, string roleSessionName);

    /// <summary>
    /// Gets AWS Credentials by assuming a role with credentials. This is useful for chaining multiple Assume Role operations. For example, you can first assume a role using web identity to get temporary credentials, and then use those credentials to assume another role to get a new set of temporary credentials with different permissions.
    /// </summary>
    /// <param name="credentials">Base AWS Credentials</param>
    /// <param name="role">AWS Role ARN to assume</param>
    /// <param name="region">AWS Region</param>
    /// <param name="roleSessionName">Role session name for the assumed role</param>
    /// <returns>Assumed AWS Credentials</returns>
    /// /// <example>
    /// <code>
    /// AWSStorageContext storageContext = await s3Client.GetAWSCredentialsWithAssumeRoleAsync("/path/to/token/file", "us-east-1", "arn:aws:iam::123456789012:role/MyRole-1", "session-name-1");
    /// storageContext.Credentials = await s3Client.GetAWSCredentialsWithAssumeRoleAsync(storageContext.Credentials, "us-west-1", "arn:aws:iam::123456789012:role/MyRole-2", "session-name-2");
    /// </code>
    /// </example>
    Task <AWSCredentials> GetAWSCredentialsWithAssumeRoleAsync(AWSCredentials credentials, string region, string role, string roleSessionName);

    /// <summary>
    /// Copies data from a source S3 bucket to a destination S3 bucket, with options to delete the source and clear the destination first.
    /// </summary>
    /// <param name="storageContext">Storage context containing AWS credentials and other relevant information</param>
    /// <param name="region">Region where the S3 buckets are located</param>
    /// <param name="sourceBucketName">Source bucket name from which data will be copied</param>
    /// <param name="sourceKeyPrefix">Source key prefix (path) within the source S3 bucket</param>
    /// <param name="destinationBucketName">Destination bucket name to which data will be copied</param>
    /// <param name="destinationKeyPrefix">Destination key prefix (path) within the destination S3 bucket</param>
    /// <param name="deleteSource">Indicates whether to delete the source data after copying</param>
    /// <param name="deleteDestinationFirst">If true, deletes all data in the destinationKeyPrefix before the copy starts</param>
    /// <remarks>
    /// <para><strong>WARNING:</strong> When <c>deleteDestinationFirst</c> is set to true, 
    /// all objects in the destination key prefix will be permanently deleted before the copy operation begins.</para>
    /// </remarks>
    Task CopyObjectsAsync(AWSStorageContext storageContext, string region, string sourceBucketName, string sourceKeyPrefix, string destinationBucketName, string destinationKeyPrefix, bool deleteSource = false, bool deleteDestinationFirst = false);

    /// <summary>
    /// Downloads all files from a specified S3 bucket and region using the provided storage context to a local directory.
    /// </summary>
    /// <param name="storageContext">Storage context containing AWS credentials and other relevant information</param>
    /// <param name="bucketName">Bucket name where the files will be downloaded from</param>
    /// <param name="region">Region where the S3 bucket is located</param>
    /// <param name="keyPrefix">Prefix to filter the files to be downloaded</param>
    /// <param name="localDirectoryPath">Local directory path where the downloaded files will be saved</param>
    Task DownloadDirectoryAsync(AWSStorageContext storageContext, string bucketName, string region, string keyPrefix, string localDirectoryPath);

    /// <summary>
    /// Downloads a file from the specified S3 bucket and region using the provided storage context.
    /// </summary>
    /// <param name="storageContext">Storage context containing AWS credentials and other relevant information</param>
    /// <param name="bucketName">Bucket name where the file will be downloaded from</param>
    /// <param name="region">Region where the S3 bucket is located</param>
    /// <param name="objectKey">Object key (path) of the file to be downloaded within the S3 bucket</param>
    /// <param name="localFilepath">Local file path where the downloaded file will be saved. Including filename and extension</param>
    Task DownloadFileAsync(AWSStorageContext storageContext, string bucketName, string region, string objectKey, string localFilepath);

    /// <summary>
    /// Uploads all files from a local directory to the specified S3 bucket and region using the provided storage context.
    /// </summary>
    /// <param name="storageContext">Storage context containing AWS credentials and other relevant information</param>
    /// <param name="bucketName">Bucket name where the files will be uploaded</param>
    /// <param name="region">Region where the S3 bucket is located</param>
    /// <param name="keyPrefix">Prefix to be added to the keys of the uploaded files</param>
    /// <param name="localDirectoryPath">Local directory path containing the files to be uploaded</param>
    /// <param name="concurrentServiceRequests">Number of concurrent service requests for uploading files</param>
    Task UploadDirectoryAsync(AWSStorageContext storageContext, string bucketName, string region, string keyPrefix, string localDirectoryPath, int concurrentServiceRequests = 10);

    /// <summary>
    /// Uploads a file to the specified S3 bucket and region using the provided storage context.
    /// </summary>
    /// <param name="storageContext">Storage context containing AWS credentials and other relevant information</param>
    /// <param name="bucketName">Bucket name where the file will be uploaded</param>
    /// <param name="region">Region where the S3 bucket is located</param>
    /// <param name="objectKey">Object key (path) within the S3 bucket</param>
    /// <param name="localFilepath">Local file path of the file to be uploaded</param>
    Task UploadFileAsync(AWSStorageContext storageContext, string bucketName, string region, string objectKey, string localFilepath);

    /// <summary>
    /// Writes a file to the specified S3 bucket and region using the provided storage context.
    /// </summary>
    /// <param name="storageContext">Storage context containing AWS credentials and other relevant information</param>
    /// <param name="bucketName">Bucket name where the file will be written</param>
    /// <param name="region">Region where the S3 bucket is located</param>
    /// <param name="objectKey">Object key (path) within the S3 bucket</param>
    /// <param name="content">MemoryStream content of the file to be written</param>
    Task UploadFileAsync(AWSStorageContext storageContext, string bucketName, string region, string objectKey, MemoryStream content);

    /// <summary>
    /// Retrieves a list of S3 objects from the specified bucket and region using the provided storage context and key prefix.
    /// </summary>
    /// <param name="storageContext">Storage context containing AWS credentials and other relevant information</param>
    /// <param name="bucketName">Bucket name where the S3 objects are located</param>
    /// <param name="region">Region where the S3 bucket is located</param>
    /// <param name="keyPrefix">Prefix to filter the S3 objects</param>
    /// <returns>List of S3 objects matching the specified key prefix</returns>
    Task<List<S3Object>> ListS3ObjectsAsync(AWSStorageContext storageContext, string bucketName, string region, string keyPrefix, bool includeDirectories = false);

    /// <summary>
    /// Retrieves information about objects in the specified S3 bucket and region using the provided storage context and key prefix.
    /// </summary>
    /// <param name="storageContext">Storage context containing AWS credentials and other relevant information</param>
    /// <param name="bucketName">Bucket name where the S3 objects are located</param>
    /// <param name="region">Region where the S3 bucket is located</param>
    /// <param name="keyPrefix">Prefix to filter the S3 objects</param>
    /// <returns>Tuple containing SHA256 checksum, size in bytes, and list of object keys</returns>
    Task<(string sha256Checksum, long sizeInBytes, List<string> objectKeys)> GetObjectsInfoAsync(AWSStorageContext storageContext, string bucketName, string region, string keyPrefix);

    /// <summary>
    /// Checks if an object(s) exists in the specified S3 bucket and region using the provided storage context.
    /// </summary>
    /// <param name="storageContext">Storage context containing AWS credentials and other relevant information</param>
    /// <param name="bucketName">Bucket name where the S3 object is located</param>
    /// <param name="region">Region where the S3 bucket is located</param>
    /// <param name="keyPrefix">Object key (path) or prefix of the S3 object to check. Note if prefix is used, the method checks for any object that starts with the prefix</param>
    /// <returns>True if the object exists, otherwise false</returns>
    Task<bool> ObjectsExistAsync(AWSStorageContext storageContext, string bucketName, string region, string keyPrefix);

    /// <summary>
    /// Deletes object(s) from the specified S3 bucket and region using the provided storage context.
    /// </summary>
    /// <param name="storageContext">Storage context containing AWS credentials and other relevant information</param>
    /// <param name="bucketName">Bucket name where the file will be deleted</param>
    /// <param name="region">Region where the S3 bucket is located</param>
    /// <param name="keyPrefix">Object key (path) or prefix of the file to be deleted within the S3 bucket. Note if prefix is used, the method deletes all objects that start with the prefix</param>
    Task DeleteObjectsAsync(AWSStorageContext storageContext, string bucketName, string region, string keyPrefix);

    /// <summary>
    /// Tests the connection to the specified S3 bucket and region using the provided storage context. It will write, read, and delete a test object to verify connectivity. Note: This is meant for testing purposes only.
    /// </summary>
    /// <param name="storageContext">Storage context containing AWS credentials and other relevant information</param>
    /// <param name="bucketName">Bucket name to test the connection against</param>
    /// <param name="region">Region where the S3 bucket is located</param>
    /// <returns>List of result messages</returns>
    Task<List<string>> TestConnectionAsync(AWSStorageContext storageContext, string bucketName, string region);
}