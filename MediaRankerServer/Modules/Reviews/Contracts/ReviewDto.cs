namespace MediaRankerServer.Modules.Reviews.Contracts;

public class ReviewFieldDto {
  public long ReviewId { get; set; }
  public long TemplateFieldId { get; set; }
  public string TemplateFieldName { get; set; } = null!;
  public int TemplateFieldPosition { get; set; }
  public short Value { get; set; } 
}

public class ReviewDto
{
  public long Id {get; set;}
  public string UserId { get; set; } = null!;
  public short OverallScore {get; set;}
  public string? ReviewTitle {get; set;}
  public string? Notes {get; set;}
  public DateTimeOffset? ConsumedAt { get; set; }
  public DateTimeOffset CreatedAt { get; set; }
  public DateTimeOffset UpdatedAt { get; set; }
  public List<ReviewFieldDto> Fields {get; set;} = [];
  // Template fields.
  public long TemplateId {get; set;}
  public string TemplateName {get; set;} = null!;
  // Media fields.
  public long? MediaId {get; set;}
  public string MediaTitle {get; set;} = null!;
  public string MediaType {get; set;} = null!;
  public DateOnly? MediaReleaseDate { get; set; }
  public string? MediaCoverImageUrl {get; set;}
  public string CoverStatus { get; set; } = "unsupported";
  public string Kind { get; set; } = "Title";
  public long? MediaCollectionId { get; set; }
  public long? SeriesId { get; set; }
  public string? SeriesTitle { get; set; }
  public int? SeasonNumber { get; set; }
  public int? EpisodeNumber { get; set; }
  public int? SeriesStartYear { get; set; }
  public int? SeriesEndYear { get; set; }
  public int? SeasonCount { get; set; }
  public int? EpisodeCount { get; set; }
}
