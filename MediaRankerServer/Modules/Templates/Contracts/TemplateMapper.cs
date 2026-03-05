using MediaRankerServer.Modules.Templates.Entities;
using MediaRankerServer.Modules.Media.Contracts;

namespace MediaRankerServer.Modules.Templates.Contracts;

public static class TemplateMapper
{
    public static TemplateDto Map(Template template)
    {
        return new TemplateDto
        {
            Id = template.Id,
            MediaType = MediaTypeMapper.Map(template.MediaType),
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
