using MediaRankerServer.Models;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MediaRankerServer.Extensions;

public static class ProblemDetailsExtensions
{
    public static IServiceCollection AddProblemDetailsHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                var httpContext = context.HttpContext;
                var problemDetails = context.ProblemDetails;
                var exception = context.Exception
                    ?? httpContext.Features.Get<IExceptionHandlerFeature>()?.Error;

                problemDetails.Instance = httpContext.Request.Path;

                if (exception is DomainException domainException)
                {
                    problemDetails.Status = StatusCodes.Status400BadRequest;
                    problemDetails.Type = domainException.Type;
                    problemDetails.Title = "Domain error";
                    problemDetails.Detail = domainException.Message;
                    return;
                }

                var errorId = Guid.NewGuid().ToString();
                var logger = httpContext.RequestServices.GetRequiredService<ILogger<Program>>();

                problemDetails.Status = StatusCodes.Status500InternalServerError;
                problemDetails.Type = "unexpected_error";
                problemDetails.Title = "Unexpected error occurred";
                problemDetails.Detail = $"Unexpected error occurred. Report this error code to your IT department: {errorId}";
                problemDetails.Extensions["errorId"] = errorId;

                logger.LogError(
                    exception,
                    "Unhandled exception. ErrorId: {ErrorId}, TraceId: {TraceId}",
                    errorId,
                    httpContext.TraceIdentifier
                );
            };
        });

        return services;
    }
}
