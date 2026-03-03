using FluentValidation;
using MediaRankerServer.Data.Entities;
using MediaRankerServer.Data.Seeds;
using MediaRankerServer.Models;
using MediaRankerServer.Models.Templates;
using Microsoft.EntityFrameworkCore;

namespace MediaRankerServer.Services;

public class TemplatesService(
    PostgreSQLContext dbContext,
    IValidator<TemplateUpsertRequest> templateUpsertRequestValidator
) : ITemplatesService
{
    public async Task<List<TemplateDto>> GetAllVisibleTemplatesAsync(string userId, CancellationToken cancellationToken = default)
    {
        var templates = await dbContext.Templates
            .AsNoTracking()
            .Include(t => t.Fields)
            .Where(t => t.UserId == SeedUtils.SystemUserId || t.UserId == userId)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return [..templates.Select(TemplateMapper.Map)];
    }

    public async Task<TemplateDto> CreateTemplateAsync(string userId, TemplateUpsertRequest request, CancellationToken cancellationToken = default)
    {
        ValidateTemplateRequestOrThrow(request);

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
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim()
        };

        foreach (var fieldRequest in request.Fields)
        {
            template.Fields.Add(new TemplateField
            {
                Name = fieldRequest.Name.Trim(),
                DisplayName = fieldRequest.DisplayName.Trim(),
                Position = fieldRequest.Position
            });
        }

        dbContext.Templates.Add(template);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetTemplateByIdAsync(template.Id, cancellationToken)
            ?? throw new DomainException("Template created but could not be loaded.", "template_load_failed");
    }

    public async Task<TemplateDto> UpdateTemplateAsync(string userId, long templateId, TemplateUpsertRequest request, CancellationToken cancellationToken = default)
    {
        ValidateTemplateRequestOrThrow(request);

        var template = await dbContext.Templates
            .Include(t => t.Fields)
            .FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken)
            ?? throw new DomainException("Template not found.", "template_not_found");

        if (template.UserId == SeedUtils.SystemUserId)
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
        template.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        template.UpdatedAt = DateTimeOffset.UtcNow;

        var requestIds = request.Fields
            .Where(f => f.Id.HasValue)
            .Select(f => f.Id!.Value)
            .ToHashSet();

        var fieldsToRemove = template.Fields
            .Where(existing => !requestIds.Contains(existing.Id))
            .ToList();

        if (fieldsToRemove.Count > 0)
        {
            // MR-15: deleting template fields will cascade-delete related ranked_media_scores,
            // but RankedMedia.OverallScore recalculation is handled in a follow-up ticket.
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
                existingField.DisplayName = fieldRequest.DisplayName.Trim();
                existingField.Position = fieldRequest.Position;
                continue;
            }

            template.Fields.Add(new TemplateField
            {
                Name = fieldRequest.Name.Trim(),
                DisplayName = fieldRequest.DisplayName.Trim(),
                Position = fieldRequest.Position
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetTemplateByIdAsync(template.Id, cancellationToken)
            ?? throw new DomainException("Template updated but could not be loaded.", "template_load_failed");
    }

    public async Task DeleteTemplateAsync(string userId, long templateId, CancellationToken cancellationToken = default)
    {
        var template = (await dbContext.Templates
            .FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken))
            ?? throw new DomainException("Template not found.", "template_not_found");

        if (template.UserId == SeedUtils.SystemUserId)
        {
            throw new DomainException("System templates cannot be deleted.", "template_forbidden");
        }

        if (template.UserId != userId)
        {
            throw new DomainException("You do not have access to this template.", "template_forbidden");
        }

        dbContext.Templates.Remove(template);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private void ValidateTemplateRequestOrThrow(TemplateUpsertRequest request)
    {
        var validationResult = templateUpsertRequestValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            throw new DomainException(validationResult.Errors[0].ErrorMessage, "template_validation_error");
        }
    }

    private async Task<TemplateDto?> GetTemplateByIdAsync(long templateId, CancellationToken cancellationToken)
    {
        var template = await dbContext.Templates
            .AsNoTracking()
            .Include(t => t.Fields)
            .Where(t => t.Id == templateId)
            .FirstOrDefaultAsync(cancellationToken);

        return template is null ? null : TemplateMapper.Map(template);
    }
}
