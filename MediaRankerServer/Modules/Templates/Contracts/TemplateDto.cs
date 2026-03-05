using MediaRankerServer.Modules.Media.Contracts;

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
