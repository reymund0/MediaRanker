namespace MediaRankerServer.Models.Templates;

public class TemplateUpsertRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<TemplateFieldUpsertRequest> Fields { get; set; } = [];
}
