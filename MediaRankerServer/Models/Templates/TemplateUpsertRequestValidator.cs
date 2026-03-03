using FluentValidation;

namespace MediaRankerServer.Models.Templates;

public class TemplateUpsertRequestValidator : AbstractValidator<TemplateUpsertRequest>
{
    public TemplateUpsertRequestValidator()
    {
        RuleFor(request => request.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Template name is required.");

        RuleFor(request => request.Fields)
            .Must(fields => fields is { Count: > 0 })
            .WithMessage("Template must include at least one field.");

        RuleFor(request => request.Fields)
            .Must(HasNonEmptyUniqueFieldNames)
            .WithMessage("Template field names must be non-empty and unique.")
            .When(request => request.Fields is { Count: > 0 });

        RuleFor(request => request.Fields)
            .Must(HasValidDisplayNames)
            .WithMessage("Template field display names are required.")
            .When(request => request.Fields is { Count: > 0 });

        RuleFor(request => request.Fields)
            .Must(HasUniquePositions)
            .WithMessage("Template field positions must be unique.")
            .When(request => request.Fields is { Count: > 0 });
    }

    private static bool HasNonEmptyUniqueFieldNames(List<TemplateFieldUpsertRequest> fields)
    {
        return fields
            .GroupBy(field => field.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .All(group => group.Key.Length > 0 && group.Count() == 1);
    }

    private static bool HasValidDisplayNames(List<TemplateFieldUpsertRequest> fields)
    {
        return fields.All(field => !string.IsNullOrWhiteSpace(field.DisplayName));
    }

    private static bool HasUniquePositions(List<TemplateFieldUpsertRequest> fields)
    {
        return fields
            .GroupBy(field => field.Position)
            .All(group => group.Count() == 1);
    }
}
