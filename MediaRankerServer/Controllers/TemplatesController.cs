using MediaRankerServer.Models.Templates;
using MediaRankerServer.Services;
using MediaRankerServer.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace MediaRankerServer.Controllers;

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
    public async Task<IActionResult> CreateTemplate([FromBody] TemplateUpsertRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetAuthenticatedUserId();
        var template = await templatesService.CreateTemplateAsync(userId, request, cancellationToken);
        return Ok(template);
    }

    [HttpPut("{templateId:long}")]
    public async Task<IActionResult> UpdateTemplate(long templateId, [FromBody] TemplateUpsertRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetAuthenticatedUserId();
        var template = await templatesService.UpdateTemplateAsync(userId, templateId, request, cancellationToken);
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
