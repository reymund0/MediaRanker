using MediaRankerServer.Models.Templates;

namespace MediaRankerServer.Services;

public interface ITemplatesService
{
    Task<List<TemplateDto>> GetAllVisibleTemplatesAsync(string userId, CancellationToken cancellationToken = default);
    Task<TemplateDto> CreateTemplateAsync(string userId, TemplateUpsertRequest request, CancellationToken cancellationToken = default);
    Task<TemplateDto> UpdateTemplateAsync(string userId, long templateId, TemplateUpsertRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteTemplateAsync(string userId, long templateId, CancellationToken cancellationToken = default);
}
