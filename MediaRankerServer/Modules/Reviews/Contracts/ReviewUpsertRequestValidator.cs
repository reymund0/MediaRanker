using FluentValidation;

namespace MediaRankerServer.Modules.Reviews.Contracts;

public class ReviewUpsertRequestValidator : AbstractValidator<ReviewUpsertRequest>
{
  public ReviewUpsertRequestValidator() {
    RuleFor(request => request.ConsumedAt)
      .Must(date => date <= DateTime.Now)
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

  private static bool HasUniqueTemplateFieldIds(List<ReviewFieldUpsertRequest> fields)
  {
    return fields.Select(field => field.TemplateFieldId).Distinct().Count() == fields.Count;
  }
}