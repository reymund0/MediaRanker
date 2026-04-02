using MediaRankerServer.Modules.Files.Data.Entities;
using FluentValidation;

namespace MediaRankerServer.Modules.Files.Contracts;

public class StartUploadRequest
{
    public string UserId { get; set; } = string.Empty;
    public long? EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty; // Maps to FileEntityType enum.
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
}

public class StartUploadRequestValidator : AbstractValidator<StartUploadRequest>
{
    public StartUploadRequestValidator()
    {       
        RuleFor(request => request.UserId)
            .NotEmpty()
            .WithMessage("User ID is required.");
            
        RuleFor(request => request.EntityType)
            .NotEmpty()
            .WithMessage("Entity type is required.")
            .Must(entityType => Enum.TryParse<FileEntityType>(entityType, true, out _))
            .WithMessage("Invalid file entity type.");

        RuleFor(request => request.FileName)
            .NotEmpty()
            .WithMessage("File name is required.");
            
        RuleFor(request => request.ContentType)
            .NotEmpty()
            .WithMessage("Content type is required.");
            
        RuleFor(request => request.FileSizeBytes)
            .GreaterThan(0)
            .WithMessage("File size must be greater than 0.");
    }
}
