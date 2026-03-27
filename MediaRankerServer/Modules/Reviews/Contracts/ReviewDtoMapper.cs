using MediaRankerServer.Modules.Reviews.Entities;
using MediaRankerServer.Modules.Files.Entities;
using MediaRankerServer.Modules.Files.Services;

namespace MediaRankerServer.Modules.Reviews.Contracts;

public static class ReviewDtoMapper
{
  public static ReviewDto Map(Review review, IFileService fileService)
  {
    string? mediaCoverImageUrl = null;
    if (review.Media.CoverFileKey != null)
    {
      mediaCoverImageUrl = fileService.GetFileUrl(review.Media.CoverFileKey, FileEntityType.MediaCover);
    }
    
    return new ReviewDto
    {
      Id = review.Id,
      UserId = review.UserId,
      OverallScore = review.OverallScore,
      ReviewTitle = review.ReviewTitle,
      Notes = review.Notes,
      ConsumedAt = review.ConsumedAt,
      CreatedAt = review.CreatedAt,
      UpdatedAt = review.UpdatedAt,
      Fields = [.. review.Fields.Select(MapField)],
      TemplateId = review.TemplateId,
      TemplateName = review.Template.Name,
      MediaId = review.MediaId,
      MediaTitle = review.Media.Title,
      MediaTypeId = review.Media.MediaTypeId,
      MediaTypeName = review.Media.MediaType.Name,
      MediaCoverImageUrl = mediaCoverImageUrl
    };
  }

  private static ReviewFieldDto MapField(ReviewField field)
  {
    return new ReviewFieldDto
    {
      ReviewId = field.ReviewId,
      TemplateFieldId = field.TemplateFieldId,
      TemplateFieldName = field.TemplateField.Name,
      Value = field.Value
    };
  }
}
