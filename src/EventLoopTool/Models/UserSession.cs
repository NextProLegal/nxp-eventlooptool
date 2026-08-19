namespace EventLoopTool.Models;

public class UserSession
{
    public string ProfessionalsAtom { get; set; } = "";
    public string SecurityClassAtom { get; set; } = "";
    public string ProfName { get; set; } = "";
    public SecurityPermissions Permissions { get; set; } = new();
    public ConnectionProfile Profile { get; set; } = new();
}
