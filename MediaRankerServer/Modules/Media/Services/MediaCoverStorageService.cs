using MediaRankerServer.Shared.Data;
using Amazon.S3;

namespace MediaRankerServer.Modules.Media.Services;

public class MediaCoverStorageService : S3Service
{
  public MediaCoverStorageService(IAmazonS3 s3Client, IConfiguration config) 
    : base(s3Client, config["AWS:MediaCoverBucket"]!)
  {
  }
}
