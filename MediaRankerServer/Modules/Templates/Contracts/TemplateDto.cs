using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Templates.Entities;

namespace MediaRankerServer.Modules.Templates.Contracts;

public class TemplateDto
{
    public long Id { get; set; }
    public bool IsSystem { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<TemplateFieldDto> Fields { get; set; } = [];
    public MediaTypeDto MediaType { get; set; } = new();
}

public class TemplateFieldDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Position { get; set; }
}


public static class TemplateDtoMapper
{
    public static TemplateDto Map(Template template)
    {
        return new TemplateDto
        {
            Id = template.Id,
            MediaType = MediaTypeDtoMapper.Map(template.MediaType),
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

    public static TemplateFieldDto MapField(TemplateField field)
    {
        return new TemplateFieldDto
        {
            Id = field.Id,
            Name = field.Name,
            Position = field.Position
        };
    }
}