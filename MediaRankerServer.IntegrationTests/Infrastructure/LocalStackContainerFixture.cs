using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Testcontainers.LocalStack;

namespace MediaRankerServer.IntegrationTests.Infrastructure;

public class LocalStackContainerFixture : IAsyncLifetime
{
    public const string AwsRegion = "us-east-1";
    public const string AwsAccessKeyId = "test";
    public const string AwsSecretAccessKey = "test";
    public const string MediaCoverBucketName = "mediaranker-media-covers-integration";

    public LocalStackContainer Container { get; } = new LocalStackBuilder("localstack/localstack:4.8")
        .Build();

    public async Task InitializeAsync()
    {
        await Container.StartAsync();

        await EnsureBucketsAsync();
    }

    public async Task DisposeAsync()
    {
        await Container.StopAsync();
    }

    public string GetS3ServiceUrl() => Container.GetConnectionString();

    private async Task EnsureBucketsAsync()
    {
        var credentials = new BasicAWSCredentials(AwsAccessKeyId, AwsSecretAccessKey);
        var config = new AmazonS3Config
        {
            ServiceURL = GetS3ServiceUrl(),
            ForcePathStyle = true,
            AuthenticationRegion = AwsRegion
        };

        using var s3Client = new AmazonS3Client(credentials, config);

        var existingBuckets = await s3Client.ListBucketsAsync();
        var buckets = existingBuckets.Buckets ?? [];
        if (buckets.Any(b => b.BucketName == MediaCoverBucketName))
        {
            return;
        }

        await s3Client.PutBucketAsync(new PutBucketRequest
        {
            BucketName = MediaCoverBucketName
        });
    }
}
