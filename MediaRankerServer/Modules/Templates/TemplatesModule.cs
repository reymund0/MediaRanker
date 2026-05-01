using FluentValidation;
using MediaRankerServer.Modules.Templates.Contracts;
using MediaRankerServer.Modules.Templates.Services;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Modules.Media.Services.Interfaces;

namespace MediaRankerServer.Modules.Templates;

public static class TemplatesModule
{
    public static IServiceCollection AddTemplatesModule(this IServiceCollection services)
    {
        services.AddScoped<ITemplateService, TemplateService>();
        services.AddScoped<IValidator<TemplateUpsertRequest>, TemplateUpsertRequestValidator>();
        return services;
    }
}
