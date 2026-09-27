using FluentValidation;
using MediaRankerServer.Modules.Reviews.Contracts;
using MediaRankerServer.Modules.Reviews.Services;
using MediaRankerServer.Modules.Templates.Services;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Modules.Media.Services.Interfaces;


namespace MediaRankerServer.Modules.Reviews;

public static class ReviewsModule
{
    public static IServiceCollection AddReviewsModule(this IServiceCollection services)
    {
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<ITemplateService, TemplateService>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IValidator<ReviewInsertRequest>, ReviewInsertRequestValidator>();
        services.AddScoped<IValidator<ReviewUpdateRequest>, ReviewUpdateRequestValidator>();
        return services;
    }
}
