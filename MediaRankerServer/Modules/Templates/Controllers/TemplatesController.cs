using MediaRankerServer.Modules.Templates.Contracts;
using MediaRankerServer.Modules.Templates.Services;
using MediaRankerServer.Shared.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace MediaRankerServer.Modules.Templates.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TemplatesController(ITemplateService templateService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetTemplates(CancellationToken cancellationToken)
    {
        var userId = User.GetAuthenticatedUserId();
        var templates = await templateService.GetAllVisibleTemplatesAsync(userId, cancellationToken);
        return Ok(templates);
    }

    [HttpGet("{mediaTypeId:long}")]
    public async Task<IActionResult> GetTemplatesByMediaType(long mediaTypeId, CancellationToken cancellationToken)
    {
        var userId = User.GetAuthenticatedUserId();
        var templates = await templateService.GetTemplatesByMediaTypeAsync(userId, mediaTypeId, cancellationToken);
        return Ok(templates);
    }

    [HttpPost]
    public async Task<IActionResult> UpsertTemplate([FromBody] TemplateUpsertRequest request, CancellationToken cancellationToken)
    {
        TemplateDto template;
        var userId = User.GetAuthenticatedUserId();
        if (request.Id is null) {
            template = await templateService.CreateTemplateAsync(userId, request, cancellationToken);       
        } else {
            template = await templateService.UpdateTemplateAsync(userId, request.Id.Value, request, cancellationToken);
        }
        
        return Ok(template);
    }

    [HttpDelete("{templateId:long}")]
    public async Task<IActionResult> DeleteTemplate(long templateId, CancellationToken cancellationToken)
    {
        var userId = User.GetAuthenticatedUserId();
        await templateService.DeleteTemplateAsync(userId, templateId, cancellationToken);
        return Ok(true);
    }

}
