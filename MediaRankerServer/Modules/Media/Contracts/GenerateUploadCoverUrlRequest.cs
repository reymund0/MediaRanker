using FluentValidation;

namespace MediaRankerServer.Modules.Media.Contracts;

public class GenerateUploadCoverUrlRequest
{
    public long? MediaId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
}

public class GenerateUploadCoverUrlRequestValidator : AbstractValidator<GenerateUploadCoverUrlRequest>
{
    public GenerateUploadCoverUrlRequestValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty()
            .WithMessage("File name is required");
        RuleFor(x => x.ContentType)
            .NotEmpty()
            .WithMessage("File content type is required")
            .Must(ct => ct.StartsWith("image/"))
            .WithMessage("File must be an image");
        RuleFor(x => x.FileSizeBytes)
            .GreaterThan(0)
            .WithMessage("File size must be greater than 0");
    }
}
