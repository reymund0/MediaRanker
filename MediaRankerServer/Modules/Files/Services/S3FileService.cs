using Microsoft.Extensions.Options;
using Amazon.S3;
using MediaRankerServer.Modules.Files.Entities;
using MediaRankerServer.Modules.Files.Contracts;
using MediaRankerServer.Modules.Files.Data;

namespace MediaRankerServer.Modules.Files.Services;

public class S3FileService : IFileService
{
    private readonly S3DataProvider _s3Service;
    private readonly IConfiguration _configuration;
    
    public S3FileService(IAmazonS3 s3Client, IConfiguration configuration)
    {
        _s3Service = new S3DataProvider(s3Client);
        _configuration = configuration;
    }
    
    public Task<StartUploadResponse> StartUploadAsync(StartUploadRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<FinishUploadResponse> FinishUploadAsync(FinishUploadRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task MarkUploadCompleteAsync(long uploadId, string userId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetFileUrlAsync(string fileKey, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task DeleteFileAsync(string fileKey, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    private string EntityTypeToBucket(FileEntityType entityType)
    {
        var key = entityType.ToString();
        return _configuration.GetValue<string>($"AWS:S3:Buckets:{key}") 
            ?? throw new InvalidOperationException($"Bucket not configured for entity type: {entityType}");
    }
}
