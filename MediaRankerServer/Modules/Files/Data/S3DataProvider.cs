using Amazon.S3;
using Amazon.S3.Model;

namespace MediaRankerServer.Modules.Files.Data;

public class S3DataProvider(IAmazonS3 s3Client)
{
  public string CreatePreviewUrl(string objectKey, string bucketName, int expiresInSeconds = 3600)
  {
    var request = new GetPreSignedUrlRequest
    {
      BucketName = bucketName,
      Key = objectKey,
      Expires = DateTime.UtcNow.AddSeconds(expiresInSeconds)
    };

    return s3Client.GetPreSignedURL(request);
  }

  public string CreateUploadUrl(string objectKey, string bucketName, string contentType, int expiresInSeconds = 300)
  {
    var request = new GetPreSignedUrlRequest
    {
      BucketName = bucketName,
      Key = objectKey,
      Expires = DateTime.UtcNow.AddSeconds(expiresInSeconds),
      Verb = HttpVerb.PUT,
      ContentType = contentType
    };

    return s3Client.GetPreSignedURL(request);
  }

  public async Task DeleteObjectAsync(string objectKey, string bucketName, CancellationToken cancellationToken)
  {
    var request = new DeleteObjectRequest
    {
      BucketName = bucketName,
      Key = objectKey
    };

    await s3Client.DeleteObjectAsync(request, cancellationToken);
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