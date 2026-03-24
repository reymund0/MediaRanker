using System.Net.Http;
using System.Net.Http.Headers;
using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.Modules.Files.Contracts;
using MediaRankerServer.Modules.Files.Entities;
using MediaRankerServer.Modules.Files.Services;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MediaRankerServer.IntegrationTests.Modules.Files;

public class S3FileServiceTests(PostgresContainerFixture postgresFixture, LocalStackContainerFixture localStackFixture) 
    : IntegrationTestBase(postgresFixture, localStackFixture)
{
    [Fact]
    public async Task StartAndFinishUpload_HappyPath_PersistsUploadMetadata()
    {
        using var scope = Factory.Services.CreateScope();
        var fileService = scope.ServiceProvider.GetRequiredService<IFileService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();

        var userId = TestAuthHandler.DefaultUserId;
        var uploadPayload = new byte[] { 1, 2, 3, 4, 5 };

        var startResponse = await fileService.StartUploadAsync(new StartUploadRequest
        {
            UserId = userId,
            EntityType = FileEntityType.MediaCover.ToString(),
            FileName = "cover-test.png",
            ContentType = "image/png",
            FileSizeBytes = uploadPayload.Length
        });

        // LocalStack uses self-signed certificates, so we need to ignore SSL validation for the test upload.
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
        using var uploadClient = new HttpClient(handler);
        using var body = new ByteArrayContent(uploadPayload);
        body.Headers.ContentType = new MediaTypeHeaderValue("image/png");

        var uploadResponse = await uploadClient.PutAsync(startResponse.UploadUrl, body);
        uploadResponse.IsSuccessStatusCode.Should().BeTrue();

        var finishResponse = await fileService.FinishUploadAsync(new FinishUploadRequest
        {
            UploadId = startResponse.UploadId,
            UserId = userId
        });

        finishResponse.UploadId.Should().Be(startResponse.UploadId);
        finishResponse.UserId.Should().Be(userId);
        finishResponse.EntityType.Should().Be(FileEntityType.MediaCover.ToString());
        finishResponse.FileName.Should().Be("cover-test.png");
        finishResponse.FileSizeBytes.Should().Be(uploadPayload.Length);

        var uploadRecord = await dbContext.FileUploads.FirstOrDefaultAsync(u => u.Id == startResponse.UploadId);
        uploadRecord.Should().NotBeNull();
        uploadRecord!.State.Should().Be(FileUploadState.Uploaded);
        uploadRecord.UserId.Should().Be(userId);
        uploadRecord.FileName.Should().Be("cover-test.png");
        uploadRecord.ActualContentType.Should().NotBeNullOrWhiteSpace();
        uploadRecord.ActualFileSizeBytes.Should().Be(uploadPayload.Length);
    }
}
