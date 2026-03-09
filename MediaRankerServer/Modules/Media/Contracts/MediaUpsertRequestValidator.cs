using FluentValidation;
using MediaRankerServer.Shared.Data;

namespace MediaRankerServer.Modules.Media.Contracts;

public class MediaUpsertRequestValidator : AbstractValidator<MediaUpsertRequest>
{
    public MediaUpsertRequestValidator(PostgreSQLContext dbContext)
    {
        RuleFor(request => request.Title)
            .Must(title => !string.IsNullOrWhiteSpace(title))
            .WithMessage("Media title is required.");

        RuleFor(request => request.MediaTypeId)
            .NotEmpty()
            .WithMessage("Media type is required.")
            .Must(id => dbContext.MediaTypes.Any(mt => mt.Id == id))
            .WithMessage("Selected media type does not exist.");

        RuleFor(request => request.ReleaseDate)
            .Must(releaseDate => releaseDate != default)
            .WithMessage("Release date is required.");
    }
}
