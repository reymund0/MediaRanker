using MediaRankerServer.Models;
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
        try
        {
            var userId = User.GetAuthenticatedUserId();
            var templates = await templatesService.GetAllVisibleTemplatesAsync(userId, cancellationToken);
            return Ok(ApiResponse<List<TemplateDto>>.Ok(templates));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<List<TemplateDto>>.Fail(ex.Message));
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateTemplate([FromBody] TemplateUpsertRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.GetAuthenticatedUserId();
            var template = await templatesService.CreateTemplateAsync(userId, request, cancellationToken);
            return Ok(ApiResponse<TemplateDto>.Ok(template));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<TemplateDto>.Fail(ex.Message));
        }
    }

    [HttpPut("{templateId:long}")]
    public async Task<IActionResult> UpdateTemplate(long templateId, [FromBody] TemplateUpsertRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.GetAuthenticatedUserId();
            var template = await templatesService.UpdateTemplateAsync(userId, templateId, request, cancellationToken);
            return Ok(ApiResponse<TemplateDto>.Ok(template));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<TemplateDto>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<TemplateDto>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<TemplateDto>.Fail(ex.Message));
        }
    }

    [HttpDelete("{templateId:long}")]
    public async Task<IActionResult> DeleteTemplate(long templateId, CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.GetAuthenticatedUserId();
            var wasDeleted = await templatesService.DeleteTemplateAsync(userId, templateId, cancellationToken);
            if (!wasDeleted)
            {
                return NotFound(ApiResponse<bool>.Fail("Template not found."));
            }

            return Ok(ApiResponse<bool>.Ok(true));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<bool>.Fail(ex.Message));
        }
    }

}
