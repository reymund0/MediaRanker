using FluentValidation;

namespace MediaRankerServer.Modules.Reviews.Contracts;

public class ReviewFieldInsertRequest
{
  public long TemplateFieldId { get; set; }
  public short Value { get; set; }
}

public class ReviewInsertRequest
{
  public long MediaId { get; set; }
  public long TemplateId { get; set; }
  public string? ReviewTitle { get; set; }
  public string? Notes { get; set; }
  public DateTimeOffset? ConsumedAt { get; set; }
  public List<ReviewFieldInsertRequest> Fields { get; set; } = [];    
}


public class ReviewInsertRequestValidator : AbstractValidator<ReviewInsertRequest>
{
  public ReviewInsertRequestValidator() {
    RuleFor(request => request.ConsumedAt)
      .Must(date => date == null || date <= DateTimeOffset.Now)
      .WithMessage("Consumed at date cannot be in the future");
    
    RuleFor(request => request.Fields)
      .Must(fields => fields.Count > 0)
      .WithMessage("At least one field is required");

    RuleFor(request => request.Fields)
      .Must(fields => fields.All(field => field.Value >= 0 && field.Value <= 10))
      .WithMessage("All scores must be between 0 and 10");

    RuleFor(request => request.Fields)
      .Must(HasUniqueTemplateFieldIds)
      .WithMessage("Cannot score the same template field multiple times");
  }

  private static bool HasUniqueTemplateFieldIds(List<ReviewFieldInsertRequest> fields)
  {
    return fields.Select(field => field.TemplateFieldId).Distinct().Count() == fields.Count;
  }
}