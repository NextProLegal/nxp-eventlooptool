namespace EventLoopTool.Models;

public class ConnectionProfile
{
    public string Name { get; set; } = "";
    public string ConnectionString { get; set; } = "";

    public override string ToString() => Name;
}
