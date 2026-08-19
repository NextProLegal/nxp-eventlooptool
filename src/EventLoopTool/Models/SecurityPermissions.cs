namespace EventLoopTool.Models;

public class SecurityPermissions
{
    public bool MassChange { get; set; }
    public bool IsAdmin { get; set; }

    public bool CanUseApp => MassChange && IsAdmin;

    public string MissingDescription
    {
        get
        {
            var missing = new List<string>();
            if (!IsAdmin) missing.Add("Admin");
            if (!MassChange) missing.Add("MassChange");
            return string.Join(", ", missing);
        }
    }

    private static bool YNToBool(string? yn) =>
        yn != null && yn.Trim().Equals("Y", StringComparison.OrdinalIgnoreCase);

    public static SecurityPermissions FromRecord(string? massChange, string? admin) =>
        new() { MassChange = YNToBool(massChange), IsAdmin = YNToBool(admin) };
}
