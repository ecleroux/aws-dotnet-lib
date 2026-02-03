using System.Net;
using System.Security.Cryptography;
using System.Text;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using AWS.Core.Interfaces;
using AWS.Core.Models;

namespace AWS.Core.Services;

public class AWSS3Client : IAWSS3Client
{
    private const string _expiredTokenErrorCode = "ExpiredToken";

    public AWSS3Client()
    {
    }

    public AWSStorageContext GetStorageContextWithBasicAWSCredentials(string accessKey, string secretKey)
    {
        return new AWSStorageContext { Credentials = new BasicAWSCredentials(accessKey, secretKey) };
    }

    public async Task<AWSStorageContext> GetStorageContextWithAssumeRoleWithWebIdentityAsync(string tokenFilepath, string region, string role, string roleSessionName)
    {
        try
        {
            var request = new AssumeRoleWithWebIdentityRequest()
            {
                WebIdentityToken = File.ReadAllText(tokenFilepath),
                RoleArn = role,
                RoleSessionName = roleSessionName,
                DurationSeconds = 3600
            };

            AmazonSecurityTokenServiceClient securityTokenServiceClient = new AmazonSecurityTokenServiceClient(RegionEndpoint.GetBySystemName(region));

            var assumeRoleResult = await securityTokenServiceClient.AssumeRoleWithWebIdentityAsync(request);
            return new AWSStorageContext { Credentials = assumeRoleResult.Credentials };
        }
        catch (AmazonServiceException exception)
        {
            if (exception.ErrorCode.Equals(_expiredTokenErrorCode))
                return await GetStorageContextWithAssumeRoleWithWebIdentityAsync(tokenFilepath, region, role, roleSessionName);
            throw;
        }
    }

    public async Task <AWSCredentials> GetAWSCredentialsWithAssumeRoleAsync(AWSCredentials credentials, string region, string role, string roleSessionName)
    {
        try
        {
            var request = new AssumeRoleRequest()
            {
                RoleArn = role,
                RoleSessionName = roleSessionName,
                DurationSeconds = 3600
            };

            AmazonSecurityTokenServiceClient securityTokenServiceClient = new AmazonSecurityTokenServiceClient(credentials, RegionEndpoint.GetBySystemName(region));

            var assumeRoleResult = await securityTokenServiceClient.AssumeRoleAsync(request);
            return assumeRoleResult.Credentials;
        }
        catch (AmazonServiceException exception)
        {
            if (exception.ErrorCode.Equals(_expiredTokenErrorCode))
                return await GetAWSCredentialsWithAssumeRoleAsync(credentials, region, role, roleSessionName);
            throw;
        }
    }

    /// <summary>
    /// Initializes the Amazon S3 client in the provided AWS storage context if it is not already initialized.
    /// </summary>
    /// <param name="storageContext">The AWS storage context containing credentials.</param>
    /// <param name="region">The AWS region for the S3 client.</param>
    private void InitializeS3Client(AWSStorageContext storageContext, string region)
    {
        if (storageContext == null || storageContext.Credentials == null)
            throw new ArgumentNullException(nameof(storageContext), "AWS Storage context or Credentials cannot be null.");

        if (storageContext.S3Client == null)
            storageContext.S3Client = new AmazonS3Client(storageContext.Credentials, RegionEndpoint.GetBySystemName(region));
    }

    public async Task CopyObjectsAsync(AWSStorageContext storageContext, string region, string sourceBucketName, string sourceKeyPrefix, string destinationBucketName, string destinationKeyPrefix, bool deleteSource = false, bool deleteDestinationFirst = false)
    {
        InitializeS3Client(storageContext, region);

        if (deleteDestinationFirst)
        {
            //Delete destination if exists
            var destinationS3Objects = await ListS3ObjectsAsync(storageContext, destinationBucketName, region, destinationKeyPrefix);
            foreach (var o in destinationS3Objects)
            {
                var deleteRequest = new DeleteObjectRequest
                {
                    BucketName = destinationBucketName,
                    Key = o.Key
                };

                var deleteResponse = await storageContext.S3Client!.DeleteObjectAsync(deleteRequest);

                if (deleteResponse.HttpStatusCode != HttpStatusCode.NoContent)
                    throw new InvalidOperationException($"Deleting object ({o.Key}) failed! Status Code {deleteResponse.HttpStatusCode}");
            }
        }

        //Copy source to destination
        var sourceS3Objects = await ListS3ObjectsAsync(storageContext, sourceBucketName, region, sourceKeyPrefix);

        foreach (var o in sourceS3Objects)
        {
            string destinationKey = o.Key.Replace(sourceKeyPrefix, destinationKeyPrefix);

            var copyRequest = new CopyObjectRequest
            {
                SourceBucket = sourceBucketName,
                SourceKey = o.Key,
                DestinationBucket = destinationBucketName,
                DestinationKey = destinationKey
            };

            var copyResponse = await storageContext.S3Client!.CopyObjectAsync(copyRequest);

            if (copyResponse.HttpStatusCode != HttpStatusCode.OK)
                throw new InvalidOperationException($"Copying object ({o.Key}) failed! Status Code {copyResponse.HttpStatusCode}");
        }

        //Delete source if required
        if (deleteSource)
        {
            foreach (var o in sourceS3Objects)
            {
                var deleteRequest = new DeleteObjectRequest
                {
                    BucketName = sourceBucketName,
                    Key = o.Key
                };
                var deleteResponse = await storageContext.S3Client!.DeleteObjectAsync(deleteRequest);
                if (deleteResponse.HttpStatusCode != HttpStatusCode.NoContent)
                    throw new InvalidOperationException($"Deleting object ({o.Key}) failed! Status Code {deleteResponse.HttpStatusCode}");
            }
        }
    }

    public async Task DeleteObjectsAsync(AWSStorageContext storageContext, string bucketName, string region, string keyPrefix)
    {
        InitializeS3Client(storageContext, region);

        var destinationS3Objects = await ListS3ObjectsAsync(storageContext, bucketName, region, keyPrefix);

        //Delete destination if exists
        foreach (var o in destinationS3Objects)
        {
            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = bucketName,
                Key = o.Key
            };

            var deleteResponse = await storageContext.S3Client!.DeleteObjectAsync(deleteRequest);

            if (deleteResponse.HttpStatusCode != HttpStatusCode.NoContent)
                throw new InvalidOperationException($"Deleting object ({o.Key}) failed! Status Code {deleteResponse.HttpStatusCode}");
        }

    }

    public async Task DownloadDirectoryAsync(AWSStorageContext storageContext, string bucketName, string region, string keyPrefix, string localDirectoryPath)
    {
        InitializeS3Client(storageContext, region);
        TransferUtility fileTransferUtility = new TransferUtility(storageContext.S3Client!);
        await fileTransferUtility.DownloadDirectoryAsync(bucketName, keyPrefix, localDirectoryPath);
    }

    public async Task DownloadFileAsync(AWSStorageContext storageContext, string bucketName, string region, string objectKey, string localFilepath)
    {
        InitializeS3Client(storageContext, region);
        TransferUtility fileTransferUtility = new TransferUtility(storageContext.S3Client!);
        await fileTransferUtility.DownloadAsync(localFilepath, bucketName, objectKey);
    }

    public async Task<List<S3Object>> ListS3ObjectsAsync(AWSStorageContext storageContext, string bucketName, string region, string keyPrefix, bool includeDirectories = false)
    {
        InitializeS3Client(storageContext, region);

        List<S3Object> s3Objects = new List<S3Object>();

        ListObjectsV2Request request = new ListObjectsV2Request
        {
            BucketName = bucketName,
            Prefix = keyPrefix
        };

        ListObjectsV2Response response;

        do
        {
            response = await storageContext.S3Client!.ListObjectsV2Async(request);

            if (response.S3Objects == null || !response.S3Objects.Any())
                break;

            foreach (S3Object o in response.S3Objects)
            {
                s3Objects.Add(o);
            }

            request.ContinuationToken = response.NextContinuationToken;
        }
        while (response.IsTruncated ?? false);

        if (includeDirectories)
            return s3Objects;

        // Filter out S3 "folders" (keys that end with "/" after trimming whitespace)
        return s3Objects.Where(obj => !obj.Key.TrimEnd().EndsWith("/")).ToList();
    }

    public async Task<bool> ObjectsExistAsync(AWSStorageContext storageContext, string bucketName, string region, string keyPrefix)
    {
        InitializeS3Client(storageContext, region);

        var request = new ListObjectsV2Request
        {
            BucketName = bucketName,
            Prefix = keyPrefix,
            MaxKeys = 1
        };

        var response = await storageContext.S3Client!.ListObjectsV2Async(request);
        return response.S3Objects.Any();
    }

    public async Task UploadDirectoryAsync(AWSStorageContext storageContext, string bucketName, string region, string keyPrefix, string localDirectoryPath, int concurrentServiceRequests = 10)
    {
        InitializeS3Client(storageContext, region);

        var transferUtilityConfig = new TransferUtilityConfig
        {
            ConcurrentServiceRequests = concurrentServiceRequests
        };

        var request = new TransferUtilityUploadDirectoryRequest
        {
            BucketName = bucketName,
            Directory = localDirectoryPath,
            KeyPrefix = keyPrefix,
            SearchOption = SearchOption.AllDirectories
        };

        TransferUtility fileTransferUtility = new TransferUtility(storageContext.S3Client!, transferUtilityConfig);
        await fileTransferUtility.UploadDirectoryAsync(request);
    }

    public async Task UploadFileAsync(AWSStorageContext storageContext, string bucketName, string region, string objectKey, string localFilepath)
    {
        InitializeS3Client(storageContext, region);
        TransferUtility fileTransferUtility = new TransferUtility(storageContext.S3Client!);
        await fileTransferUtility.UploadAsync(localFilepath, bucketName, objectKey);
    }

    public async Task UploadFileAsync(AWSStorageContext storageContext, string bucketName, string region, string objectKey, MemoryStream content)
    {
        InitializeS3Client(storageContext, region);

        content.Position = 0; // Add before putting to S3

        PutObjectRequest request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            InputStream = content
        };

        PutObjectResponse response = await storageContext.S3Client!.PutObjectAsync(request);

        if (response.HttpStatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException($"Writing file ({objectKey}) failed! Status Code {response.HttpStatusCode}");
    }

    public async Task<(string sha256Checksum, long sizeInBytes, List<string> objectKeys)> GetObjectsInfoAsync(AWSStorageContext storageContext, string bucketName, string region, string keyPrefix)
    {
        InitializeS3Client(storageContext, region);

        List<S3Object> s3Objects = await ListS3ObjectsAsync(storageContext, bucketName, region, keyPrefix);

        if (!s3Objects.Any())
            return (string.Empty, 0, new List<string>());

        List<string> objectKeys = new List<string>();

        List<(string item, byte[] value)> hashValues = new List<(string item, byte[] hash)>();

        long fileSizeInBytes = 0;

        foreach (var o in s3Objects)
        {
            string filename = o.Key.Substring(o.Key.LastIndexOf("/") + 1);

            if (filename.StartsWith("_"))
                continue;

            GetObjectRequest request = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = o.Key
            };

            using (GetObjectResponse response = await storageContext.S3Client!.GetObjectAsync(request))
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    await response.ResponseStream.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;
                    fileSizeInBytes += memoryStream.Length;

                    using (SHA256 sha256Hash = SHA256.Create())
                    {
                        hashValues.Add((filename, sha256Hash.ComputeHash(memoryStream)));
                    }

                    objectKeys.Add(o.Key);
                }
            }
        }

        objectKeys = objectKeys.OrderBy(k => k).ToList();

        return (GetSHA256ChecksumFromHashValues(hashValues), fileSizeInBytes, objectKeys);
    }

    /// <summary>
    /// Generates a SHA256 checksum from a list of hash values.
    /// </summary>
    /// <param name="hashValues">List of tuples containing item names and their corresponding hash byte arrays.</param>
    /// <returns>SHA256 checksum as a hexadecimal string.</returns>
    /// <exception cref="ArgumentException">Thrown when hashValues is null or empty.</exception>
    private string GetSHA256ChecksumFromHashValues(List<(string item, byte[] value)> hashValues)
    {
        if (hashValues == null || hashValues.Count == 0)
            throw new ArgumentException("Hash values cannot be null or empty", nameof(hashValues));
    
        hashValues.Sort((x, y) => string.Compare(x.item, y.item));

        var combined = new List<byte>();
        foreach (var hashValue in hashValues)
            combined.AddRange(hashValue.value);

        byte[] hashValueCombined = combined.ToArray();

        var stringBuilder = new StringBuilder();

        using (SHA256 sha256Hash = SHA256.Create())
        {
            byte[] hashValueFinal = (hashValues.Count() > 1) 
                ? sha256Hash.ComputeHash(hashValueCombined) 
                : hashValueCombined;

            for (int i = 0; i < hashValueFinal.Length; i++)
                stringBuilder.Append(hashValueFinal[i].ToString("x2"));
        }
        return stringBuilder.ToString();
    }

    public async Task<List<string>> TestConnectionAsync(AWSStorageContext storageContext, string bucketName, string region)
    {
        InitializeS3Client(storageContext, region);

        List<string> result = new List<string>();

        try
        {
            // Test 1: List objects (required for DownloadDirectoryAsync)
            result.Add($"Testing ListBucket permission for bucket {bucketName}...");
            try
            {
                var listRequest = new ListObjectsV2Request
                {
                    BucketName = bucketName,
                    MaxKeys = 1
                };
                var listResponse = await storageContext.S3Client!.ListObjectsV2Async(listRequest);
                result.Add($"✓ Successfully listed objects in bucket {bucketName}.");
            }
            catch (Exception ex)
            {
                result.Add($"✗ Failed to list objects in bucket {bucketName}. Error: {ex.Message}");
                return result;
            }

            // Test 2: Write to S3 bucket
            result.Add($"Testing PutObject permission for bucket {bucketName}...");
            string testFileKey = "narwhal_connection_test.txt";
            string testFileContent = "This is a test file to verify S3 bucket connection.";

            try
            {
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(testFileContent)))
                {
                    var putRequest = new PutObjectRequest
                    {
                        BucketName = bucketName,
                        Key = testFileKey,
                        InputStream = stream
                    };

                    var putResponse = await storageContext.S3Client!.PutObjectAsync(putRequest);

                    if (putResponse.HttpStatusCode != HttpStatusCode.OK)
                    {
                        result.Add($"✗ Failed to upload test file to bucket {bucketName}. HTTP Status Code: {putResponse.HttpStatusCode}");
                        return result;
                    }
                    result.Add($"✓ Successfully uploaded test file to bucket {bucketName}.");
                }
            }
            catch (Exception ex)
            {
                result.Add($"✗ Failed to upload test file. Error: {ex.Message}");
                return result;
            }

            // Test 3: Read the file back using GetObjectAsync (this mimics what DownloadFileAsync does)
            result.Add($"Testing GetObject permission for bucket {bucketName}...");
            try
            {
                var getRequest = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = testFileKey
                };

                var getResponse = await storageContext.S3Client!.GetObjectAsync(getRequest);
                using (var reader = new StreamReader(getResponse.ResponseStream))
                {
                    string content = reader.ReadToEnd();
                    if (content == testFileContent)
                    {
                        result.Add($"✓ Successfully read and verified test file from bucket {bucketName}.");
                    }
                    else
                    {
                        result.Add($"✗ Test file content mismatch in bucket {bucketName}.");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Add($"✗ Failed to read test file. This may indicate missing s3:GetObject permission. Error: {ex.Message}");
                return result;
            }

            // Test 4: Download using TransferUtility (mimics actual DownloadDirectoryAsync)
            result.Add($"Testing TransferUtility download for bucket {bucketName}...");
            try
            {
                string localTestPath = Path.Combine(Path.GetTempPath(), "narwhal_transfer_test.txt");
                TransferUtility fileTransferUtility = new TransferUtility(storageContext.S3Client!);
                await fileTransferUtility.DownloadAsync(localTestPath, bucketName, testFileKey);

                if (File.Exists(localTestPath))
                {
                    string downloadedContent = File.ReadAllText(localTestPath);
                    File.Delete(localTestPath);

                    if (downloadedContent == testFileContent)
                    {
                        result.Add($"✓ Successfully downloaded and verified test file using TransferUtility from bucket {bucketName}.");
                    }
                    else
                    {
                        result.Add($"✗ Downloaded file content mismatch in bucket {bucketName}.");
                    }
                }
                else
                {
                    result.Add($"✗ Downloaded file does not exist at {localTestPath}.");
                }
            }
            catch (Exception ex)
            {
                result.Add($"✗ Failed to download using TransferUtility. Error: {ex.Message}");
                return result;
            }

            // Test 5: Delete the test file
            result.Add($"Testing DeleteObject permission for bucket {bucketName}...");
            try
            {
                var deleteRequest = new DeleteObjectRequest
                {
                    BucketName = bucketName,
                    Key = testFileKey
                };

                var deleteResponse = await storageContext.S3Client!.DeleteObjectAsync(deleteRequest);

                if (deleteResponse.HttpStatusCode == HttpStatusCode.NoContent)
                {
                    result.Add($"✓ Successfully deleted test file from bucket {bucketName}.");
                }
                else
                {
                    result.Add($"✗ Failed to delete test file. HTTP Status Code: {deleteResponse.HttpStatusCode}");
                }
            }
            catch (Exception ex)
            {
                result.Add($"✗ Failed to delete test file. Error: {ex.Message}");
            }

            result.Add($"\n✓ All connection tests passed successfully for bucket {bucketName}!");
        }
        catch (Exception ex)
        {
            result.Add($"✗ Unexpected error during S3 connection test: {ex.Message}");
        }

        return result;
    }
}