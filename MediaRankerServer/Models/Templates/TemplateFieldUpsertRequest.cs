namespace MediaRankerServer.Models.Templates;

public class TemplateFieldUpsertRequest
{
    public long? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int Position { get; set; }
}
