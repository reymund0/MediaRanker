using FluentValidation;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Shared.Data;

namespace MediaRankerServer.Modules.Media.Contracts;

public class MediaCollectionUpsertRequest
{
    public long? Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public MediaCollectionType CollectionType { get; set; }
    public long MediaTypeId { get; set; }
    public long? ParentMediaCollectionId { get; set; }
    public DateOnly ReleaseDate { get; set; }
    public long? CoverUploadId { get; set; }
}

public class MediaCollectionUpsertRequestValidator : AbstractValidator<MediaCollectionUpsertRequest>
{
    public MediaCollectionUpsertRequestValidator(PostgreSQLContext dbContext)
    {
        RuleFor(request => request.Title)
            .Must(title => !string.IsNullOrWhiteSpace(title))
            .WithMessage("Collection title is required.");

        RuleFor(request => request.CollectionType)
            .IsInEnum()
            .WithMessage("Collection type is invalid.");

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
