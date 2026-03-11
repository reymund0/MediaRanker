using FluentValidation;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Modules.Templates.Services;
using MediaRankerServer.Modules.Rankings.Contracts;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Exceptions;
namespace MediaRankerServer.Modules.Rankings.Services;

public class RankedMediaService(
  PostgreSQLContext dbContext,
  IValidator<RankedMediaUpsertRequest> rankedMediaUpsertRequestValidator,
  IMediaService mediaService,
  ITemplatesService templatesService
  ) : IRankedMediaService
{
    public Task<List<RankedMediaDto>> GetRankedMediaAsync(string userId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task<RankedMediaDto> CreateRankedMediaAsync(string userId, RankedMediaUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateRankedMediaUpsertRequestOrThrowAsync(request, cancellationToken);
        throw new NotImplementedException();
    }

    public Task<RankedMediaDto> UpdateRankedMediaAsync(string userId, long rankedMediaId, RankedMediaUpsertRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task DeleteRankedMediaAsync(string userId, long rankedMediaId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    private async Task ValidateRankedMediaUpsertRequestOrThrowAsync(RankedMediaUpsertRequest request, CancellationToken cancellationToken = default)
    {
        const string errorType = "ranked_media_upsert_validation_error";

        // Validate request
        await rankedMediaUpsertRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        // Validate Media exists
        _ = await mediaService.GetMediaByIdAsync(request.MediaId, cancellationToken) ?? throw new DomainException($"Media {request.MediaId} not found", errorType);

        // Validate Template exists
        var template = await templatesService.GetTemplateByIdAsync(request.TemplateId, cancellationToken) ?? throw new DomainException($"Template {request.TemplateId} not found", errorType);
        
        // Validate Template fields are valid.
        var invalidField = request.Scores.FirstOrDefault(score => !template.Fields.Any(field => field.Id == score.TemplateFieldId));
        if (invalidField is not null)
        {
            throw new DomainException($"Template field {invalidField.TemplateFieldId} not found in template {request.TemplateId}", errorType);
        }
    }
}
