namespace MediaRankerServer.Models;

public class DomainException : Exception
{
    public string Type { get; }

    public DomainException(string message, string? type = "domain_error") : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Type = string.IsNullOrWhiteSpace(type) ? "domain_error" : type;
    }
}
