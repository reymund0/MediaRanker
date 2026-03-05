using MediaRankerServer.Modules.Templates.Entities;

namespace MediaRankerServer.Modules.Templates.Contracts;

public static class TemplateMapper
{
    public static MediaRankerServer.Modules.Templates.Contracts.TemplateDto Map(MediaRankerServer.Modules.Templates.Entities.Template template)
    {
        return new MediaRankerServer.Modules.Templates.Contracts.TemplateDto
        {
            Id = template.Id,
            IsSystem = template.Id < 0,
            UserId = template.UserId,
            Name = template.Name,
            Description = template.Description,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt,
            Fields =
            [
                .. template.Fields.Select(MapField)
            ]
        };
    }

    private static TemplateFieldDto MapField(TemplateField field)
    {
        return new TemplateFieldDto
        {
            Id = field.Id,
            Name = field.Name,
            Position = field.Position
        };
    }
}
