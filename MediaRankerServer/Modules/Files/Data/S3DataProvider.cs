using Amazon.S3;
using Amazon.S3.Model;

namespace MediaRankerServer.Modules.Files.Data;

public class S3DataProvider(IAmazonS3 s3Client)
{
  public async Task<string> CreatePreviewUrlAsync(string objectKey, string bucketName, int expiresInSeconds = 3600)
  {
    var request = new GetPreSignedUrlRequest
    {
      BucketName = bucketName,
      Key = objectKey,
      Expires = DateTime.UtcNow.AddSeconds(expiresInSeconds)
    };

    return await s3Client.GetPreSignedURLAsync(request);
  }

  public async Task<string> CreateUploadUrlAsync(string objectKey, string bucketName, int expiresInSeconds = 300)
  {
    var request = new GetPreSignedUrlRequest
    {
      BucketName = bucketName,
      Key = objectKey,
      Expires = DateTime.UtcNow.AddSeconds(expiresInSeconds),
      Verb = HttpVerb.PUT // Indicates this is an upload request
    };

    return await s3Client.GetPreSignedURLAsync(request);
  }

  public async Task<GetObjectMetadataResponse> GetObjectMetadataAsync(string objectKey, string bucketName, CancellationToken cancellationToken)
  {
    var request = new GetObjectMetadataRequest
    {
      BucketName = bucketName,
      Key = objectKey
    };

    return await s3Client.GetObjectMetadataAsync(request, cancellationToken);
  }

  public async Task<bool> DoesObjectExistAsync(string objectKey, string bucketName, CancellationToken cancellationToken)
  {
    var request = new GetObjectAttributesRequest
    {
      BucketName = bucketName,
      Key = objectKey
    };

    try
    {
      await s3Client.GetObjectAttributesAsync(request, cancellationToken);
      return true;
    }
    catch
    {
      return false;
    }
  }
}