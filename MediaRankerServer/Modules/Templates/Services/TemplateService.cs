using FluentValidation;
using MediaRankerServer.Modules.Media.Services;
using MediatR;
using MediaRankerServer.Modules.Templates.Entities;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Exceptions;
using MediaRankerServer.Modules.Templates.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MediaRankerServer.Modules.Templates.Services;

public class TemplateService(
    PostgreSQLContext dbContext,
    IValidator<TemplateUpsertRequest> templateUpsertRequestValidator,
    IPublisher publisher,
    IMediaService mediaService
) : ITemplateService
{
    public async Task<List<TemplateDto>> GetAllVisibleTemplatesAsync(string userId, CancellationToken cancellationToken = default)
    {
        var templates = await dbContext.Templates
            .AsNoTracking()
            .Include(t => t.Fields)
            .Include(t => t.MediaType)
            .Where(t => t.Id < 0 || t.UserId == userId)
            .ToListAsync(cancellationToken);

        return [..templates.Select(TemplateDtoMapper.Map)];
    }
    
    public async Task<TemplateDto?> GetTemplateByIdAsync(long templateId, CancellationToken cancellationToken)
    {
        var template = await dbContext.Templates
            .AsNoTracking()
            .Include(t => t.Fields)
            .Include(t => t.MediaType)
            .Where(t => t.Id == templateId)
            .FirstOrDefaultAsync(cancellationToken);

        return template is null ? null : TemplateDtoMapper.Map(template);
    }

    public async Task<TemplateDto> CreateTemplateAsync(string userId, TemplateUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateTemplateRequestOrThrow(request, cancellationToken);

        var normalizedName = request.Name.Trim();
        var nameTaken = await dbContext.Templates.AnyAsync(
            t => t.UserId == userId && t.Name == normalizedName,
            cancellationToken
        );

        if (nameTaken)
        {
            throw new DomainException("Template name already exists for this user.", "template_name_conflict");
        }

        var template = new Template
        {
            UserId = userId,
            Name = normalizedName,
            MediaTypeId = request.MediaTypeId,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Fields = [..request.Fields.Select(fieldRequest => new TemplateField
            {
                Name = fieldRequest.Name.Trim(),
                Position = fieldRequest.Position
            })]
        };

        dbContext.Templates.Add(template);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetTemplateByIdAsync(template.Id, cancellationToken)
            ?? throw new DomainException("Template created but could not be loaded.", "template_load_failed");
    }

    public async Task<TemplateDto> UpdateTemplateAsync(string userId, long templateId, TemplateUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateTemplateRequestOrThrow(request, cancellationToken);

        var template = await dbContext.Templates
            .Include(t => t.Fields)
            .FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken)
            ?? throw new DomainException("Template not found.", "template_not_found");

        if (template.Id < 0)
        {
            throw new DomainException("System templates cannot be modified.", "template_forbidden");
        }

        if (template.UserId != userId)
        {
            throw new DomainException("You do not have access to this template.", "template_forbidden");
        }

        var normalizedName = request.Name.Trim();
        var nameTaken = await dbContext.Templates.AnyAsync(
            t => t.UserId == userId && t.Id != templateId && t.Name == normalizedName,
            cancellationToken
        );

        if (nameTaken)
        {
            throw new DomainException("Template name already exists for this user.", "template_name_conflict");
        }

        template.Name = normalizedName;
        template.MediaTypeId = request.MediaTypeId;
        template.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        template.UpdatedAt = DateTimeOffset.UtcNow;

        var requestIds = request.Fields
            .Where(f => f.Id.HasValue)
            .Select(f => f.Id!.Value)
            .ToHashSet();

        var fieldsToRemove = template.Fields
            .Where(existing => !requestIds.Contains(existing.Id))
            .ToList();

        var deletedFieldIds = fieldsToRemove.Select(f => f.Id).ToList();

        if (fieldsToRemove.Count > 0)
        {
            dbContext.TemplateFields.RemoveRange(fieldsToRemove);
        }

        var existingById = template.Fields.ToDictionary(f => f.Id);

        foreach (var fieldRequest in request.Fields)
        {
            if (fieldRequest.Id.HasValue)
            {
                if (!existingById.TryGetValue(fieldRequest.Id.Value, out var existingField))
                {
                    throw new DomainException($"Template field id {fieldRequest.Id.Value} was not found on this template.", "template_field_invalid");
                }

                existingField.Name = fieldRequest.Name.Trim();
                existingField.Position = fieldRequest.Position;
                continue;
            }

            template.Fields.Add(new TemplateField
            {
                Name = fieldRequest.Name.Trim(),
                Position = fieldRequest.Position
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (deletedFieldIds.Count > 0)
        {
            await publisher.Publish(new Events.TemplateFieldsDeletedEvent(template.Id, deletedFieldIds), cancellationToken);
        }

        return await GetTemplateByIdAsync(template.Id, cancellationToken)
            ?? throw new DomainException("Template updated but could not be loaded.", "template_load_failed");
    }

    public async Task DeleteTemplateAsync(string userId, long templateId, CancellationToken cancellationToken = default)
    {
        var template = (await dbContext.Templates
            .FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken))
            ?? throw new DomainException("Template not found.", "template_not_found");

        if (template.Id < 0)
        {
            throw new DomainException("System templates cannot be deleted.", "template_forbidden");
        }

        if (template.UserId != userId)
        {
            throw new DomainException("You do not have access to this template.", "template_forbidden");
        }

        // Delete Template and its fields.
        dbContext.TemplateFields.RemoveRange(dbContext.TemplateFields.Where(tf => tf.TemplateId == templateId));
        dbContext.Templates.Remove(template);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateTemplateRequestOrThrow(TemplateUpsertRequest request, CancellationToken cancellationToken)
    {
        var validationResult = templateUpsertRequestValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            throw new DomainException(validationResult.Errors[0].ErrorMessage, "template_validation_error");
        }

        // Verify media type exists
        _ = await mediaService.GetMediaTypeByIdAsync(request.MediaTypeId, cancellationToken)
            ?? throw new DomainException("Selected media type does not exist.", "template_validation_error");
    }
}
