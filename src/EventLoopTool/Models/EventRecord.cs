namespace EventLoopTool.Models;

public class EventRecord
{
    public string Events { get; set; } = "";
    public string? EventsParent { get; set; }
    public int? EventsNo { get; set; }
    public string? RTF { get; set; }
    public string? EventKind { get; set; }
    public string? EventDate { get; set; }
    public string? EventTypes { get; set; }
    public string? ProfSet { get; set; }
    public string? ShortNote { get; set; }
}
