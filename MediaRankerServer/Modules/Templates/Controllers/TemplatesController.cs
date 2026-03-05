using MediaRankerServer.Modules.Templates.Contracts;
using MediaRankerServer.Modules.Templates.Services;
using MediaRankerServer.Shared.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace MediaRankerServer.Modules.Templates.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TemplatesController(ITemplatesService templatesService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetTemplates(CancellationToken cancellationToken)
    {
        var userId = User.GetAuthenticatedUserId();
        var templates = await templatesService.GetAllVisibleTemplatesAsync(userId, cancellationToken);
        return Ok(templates);
    }

    [HttpPost]
    public async Task<IActionResult> UpsertTemplate([FromBody] TemplateUpsertRequest request, CancellationToken cancellationToken)
    {
        TemplateDto template;
        var userId = User.GetAuthenticatedUserId();
        if (request.Id is null) {
            template = await templatesService.CreateTemplateAsync(userId, request, cancellationToken);       
        } else {
            template = await templatesService.UpdateTemplateAsync(userId, request.Id.Value, request, cancellationToken);
        }
        
        return Ok(template);
    }

    [HttpDelete("{templateId:long}")]
    public async Task<IActionResult> DeleteTemplate(long templateId, CancellationToken cancellationToken)
    {
        var userId = User.GetAuthenticatedUserId();
        await templatesService.DeleteTemplateAsync(userId, templateId, cancellationToken);
        return Ok(true);
    }

}
