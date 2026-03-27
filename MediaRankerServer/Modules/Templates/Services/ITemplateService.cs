using MediaRankerServer.Modules.Templates.Contracts;

namespace MediaRankerServer.Modules.Templates.Services;

public interface ITemplateService
{
    Task<List<TemplateDto>> GetAllVisibleTemplatesAsync(string userId, CancellationToken cancellationToken = default);
    Task<TemplateDto?> GetTemplateByIdAsync(long templateId, CancellationToken cancellationToken = default);
    Task<List<TemplateDto>> GetTemplatesByMediaTypeAsync(string userId, long mediaTypeId, CancellationToken cancellationToken = default);
    Task<TemplateDto> CreateTemplateAsync(string userId, TemplateUpsertRequest request, CancellationToken cancellationToken = default);
    Task<TemplateDto> UpdateTemplateAsync(string userId, long templateId, TemplateUpsertRequest request, CancellationToken cancellationToken = default);
    Task DeleteTemplateAsync(string userId, long templateId, CancellationToken cancellationToken = default);
}
