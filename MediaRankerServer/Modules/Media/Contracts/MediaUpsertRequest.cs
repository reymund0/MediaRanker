using FluentValidation;
using MediaRankerServer.Shared.Data;

namespace MediaRankerServer.Modules.Media.Contracts;

public class MediaUpsertRequest
{
    public long? Id { get; set; }
    public long? CoverUploadId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public DateOnly ReleaseDate { get; set; }
}


public class MediaUpsertRequestValidator : AbstractValidator<MediaUpsertRequest>
{
    public MediaUpsertRequestValidator()
    {
        RuleFor(request => request.Title)
            .Must(title => !string.IsNullOrWhiteSpace(title))
            .WithMessage("Media title is required.");

        RuleFor(request => request.MediaType)
            .Must(MediaTypes.IsValid)
            .WithMessage("Media type not found.");

        RuleFor(request => request.ReleaseDate)
            .Must(releaseDate => releaseDate != default)
            .WithMessage("Release date is required.");
    }
}
