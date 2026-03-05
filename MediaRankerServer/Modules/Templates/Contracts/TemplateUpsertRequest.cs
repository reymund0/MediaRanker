namespace MediaRankerServer.Modules.Templates.Contracts;

public class TemplateUpsertRequest
{
    public long? Id {get; set;}
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<TemplateFieldUpsertRequest> Fields { get; set; } = [];
}
