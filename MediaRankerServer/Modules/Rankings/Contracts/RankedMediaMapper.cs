using MediaRankerServer.Modules.Rankings.Entities;

namespace MediaRankerServer.Modules.Rankings.Contracts;

public static class RankedMediaMapper
{
  public static RankedMediaDto Map(RankedMedia rankedMedia)
  {
    return new RankedMediaDto
    {
      Id = rankedMedia.Id,
      UserId = rankedMedia.UserId,
      OverallScore = rankedMedia.OverallScore,
      ReviewTitle = rankedMedia.ReviewTitle,
      Notes = rankedMedia.Notes,
      ConsumedAt = rankedMedia.ConsumedAt,
      CreatedAt = rankedMedia.CreatedAt,
      UpdatedAt = rankedMedia.UpdatedAt,
      Scores = [
        ..rankedMedia.Scores.Select(MapScore)
      ],
      TemplateId = rankedMedia.TemplateId,
      TemplateName = rankedMedia.Template.Name,
      MediaId = rankedMedia.MediaId,
      MediaTitle = rankedMedia.Media.Title
    };
  }

  private static RankedMediaScoreDto MapScore(RankedMediaScore score)
  {
    return new RankedMediaScoreDto
    {
      RankedMediaId = score.RankedMediaId,
      TemplateFieldId = score.TemplateFieldId,
      Value = score.Value
    };
  }
}