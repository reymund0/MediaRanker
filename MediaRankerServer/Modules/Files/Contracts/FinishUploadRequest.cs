using FluentValidation;

namespace MediaRankerServer.Modules.Files.Contracts;

public class FinishUploadRequest
{
    public long UploadId { get; set; }
    public string UserId { get; set; } = string.Empty;
}

public class FinishUploadRequestValidator : AbstractValidator<FinishUploadRequest>
{
    public FinishUploadRequestValidator()
    {
        RuleFor(request => request.UploadId)
            .GreaterThan(0)
            .WithMessage("Upload ID is required.");
        
        RuleFor(request => request.UserId)
            .NotEmpty()
            .WithMessage("User ID is required.");
    }
}
