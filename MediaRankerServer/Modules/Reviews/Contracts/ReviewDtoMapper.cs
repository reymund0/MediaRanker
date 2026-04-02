using MediaRankerServer.Modules.Reviews.Data.Entities;
using MediaRankerServer.Modules.Reviews.Data.Views;
using MediaRankerServer.Modules.Files.Data.Entities;
using MediaRankerServer.Modules.Files.Services;

namespace MediaRankerServer.Modules.Reviews.Contracts;

public static class ReviewDtoMapper
{
  public static ReviewDto Map(IFileService fileService, ReviewDetailView review, IEnumerable<ReviewFieldDetails> fields)
  {
    var mediaCoverImageUrl = string.Empty;
    if (review.MediaCoverFileKey != null)
    {
      mediaCoverImageUrl = fileService.GetFileUrl(review.MediaCoverFileKey, FileEntityType.MediaCover);
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
      Fields = [.. fields.Select(MapField)],
      TemplateId = review.TemplateId,
      TemplateName = review.TemplateName,
      MediaId = review.MediaId,
      MediaTitle = review.MediaTitle,
      MediaTypeId = review.MediaTypeId,
      MediaTypeName = review.MediaTypeName,
      MediaCoverImageUrl = mediaCoverImageUrl
    };
  }

  private static ReviewFieldDto MapField(ReviewFieldDetails reviewField)
  {
    
    return new ReviewFieldDto
    {
      ReviewId = reviewField.Field.ReviewId,
      TemplateFieldId = reviewField.Field.TemplateFieldId,
      TemplateFieldName = reviewField.TemplateFieldName ?? "Unknown Field",
      TemplateFieldPosition = reviewField.TemplateFieldPosition,
      Value = reviewField.Field.Value
    };
  }

  public record ReviewFieldDetails(ReviewField Field, string TemplateFieldName, int TemplateFieldPosition);
}
