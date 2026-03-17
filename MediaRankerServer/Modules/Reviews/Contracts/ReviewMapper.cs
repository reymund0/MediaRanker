using MediaRankerServer.Modules.Reviews.Entities;

namespace MediaRankerServer.Modules.Reviews.Contracts;

public static class ReviewMapper
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
      Scores = review.Scores.Select(MapScore).ToList(),
      TemplateId = review.TemplateId,
      TemplateName = review.Template.Name,
      MediaId = review.MediaId,
      MediaTitle = review.Media.Title,
      MediaTypeId = review.Media.MediaTypeId,
      MediaTypeName = review.Media.MediaType.Name
    };
  }

  private static ReviewFieldDto MapScore(ReviewField score)
  {
    return new ReviewFieldDto
    {
      ReviewId = score.ReviewId,
      TemplateFieldId = score.TemplateFieldId,
      Value = score.Value
    };
  }
}