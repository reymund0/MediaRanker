using MediaRankerServer.Modules.Reviews.Entities;

namespace MediaRankerServer.Modules.Reviews.Contracts;

public static class ReviewDtoMapper
{
  public static ReviewDto Map(Review review)
  {
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
      Fields = review.Fields.Select(MapField).ToList(),
      TemplateId = review.TemplateId,
      TemplateName = review.Template.Name,
      MediaId = review.MediaId,
      MediaTitle = review.Media.Title,
      MediaTypeId = review.Media.MediaTypeId,
      MediaTypeName = review.Media.MediaType.Name
    };
  }

  private static ReviewFieldDto MapField(ReviewField field)
  {
    return new ReviewFieldDto
    {
      ReviewId = field.ReviewId,
      TemplateFieldId = field.TemplateFieldId,
      Value = field.Value
    };
  }
}