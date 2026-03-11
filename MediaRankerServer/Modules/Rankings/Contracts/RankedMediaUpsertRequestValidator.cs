using FluentValidation;

namespace MediaRankerServer.Modules.Rankings.Contracts;

public class RankedMediaUpsertRequestValidator : AbstractValidator<RankedMediaUpsertRequest>
{
  public RankedMediaUpsertRequestValidator() {
    RuleFor(request => request.ConsumedAt)
      .Must(date => date <= DateTime.Now)
      .WithMessage("Consumed at date cannot be in the future");
    
    RuleFor(request => request.Scores)
      .Must(scores => scores.Count > 0)
      .WithMessage("At least one score is required");

    RuleFor(request => request.Scores)
      .Must(scores => scores.All(score => score.Value >= 0 && score.Value <= 10))
      .WithMessage("All scores must be between 0 and 10");

    RuleFor(request => request.Scores)
      .Must(HasUniqueTemplateFieldIds)
      .WithMessage("Cannot score the same template field multiple times");
  }

  private static bool HasUniqueTemplateFieldIds(List<RankedMediaScoreUpsertRequest> scores)
  {
    return scores.Select(score => score.TemplateFieldId).Distinct().Count() == scores.Count;
  }
}