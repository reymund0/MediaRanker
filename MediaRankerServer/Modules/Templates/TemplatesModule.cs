using FluentValidation;
using MediaRankerServer.Modules.Templates.Contracts;
using MediaRankerServer.Modules.Templates.Services;
using Microsoft.Extensions.DependencyInjection;

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
