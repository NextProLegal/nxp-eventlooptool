namespace EventLoopTool.Models;

public enum LoopType { Self, TwoNode, NNode }

public class Loop
{
    public LoopType Type { get; set; }
    public List<EventRecord> Nodes { get; set; } = new();
}

public class DisplayRow
{
    public string EventId { get; set; } = "";
    public string? ParentId { get; set; }
    public string? EventNo { get; set; }
    public string? ParentNo { get; set; }
    public string Matter { get; set; } = "\u2014";
    public string? EventDate { get; set; }
    public string Kind { get; set; } = "\u2014";
    public string EventType { get; set; } = "\u2014";
    public string Professional { get; set; } = "\u2014";
    public string Note { get; set; } = "";
}

public class DisplayLoop
{
    public LoopType Type { get; set; }
    public string ChainLabel { get; set; } = "";
    public List<DisplayRow> Rows { get; set; } = new();

    public static string TypeLabel(LoopType type) => type switch
    {
        LoopType.Self => "Self-reference",
        LoopType.TwoNode => "2-node cycle",
        LoopType.NNode => "N-node cycle",
        _ => "Unknown"
    };
}
