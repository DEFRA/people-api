using System.ComponentModel.DataAnnotations.Schema;

namespace Defra_People_API.Entities;

public enum LogLevel
{
    Information,
    Warning,
    Error
}

public class LogItem
{
    public LogItem()
    {
        Timestamp = DateTime.UtcNow;
    }

    public int Id { get; set; }
    public LogLevel LogLevel { get; set; }
    public string Consumer { get; set; } = null!;
    public string Method { get; set; } = null!;
    public string Endpoint { get; set; } = null!;
    public string User { get; set; } = null!;
    public string Params { get; set; } = null!;
    public string Response { get; set; } = null!;
    public TimeSpan Runtime { get; set; }
    public DateTime Timestamp { get; set; }
}
