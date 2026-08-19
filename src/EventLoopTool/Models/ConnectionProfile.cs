using System.Xml.Serialization;

namespace EventLoopTool.Models;

public class ConnectionProfile
{
    public string Name { get; set; } = "";
    public string Server { get; set; } = "";
    public string Database { get; set; } = "";
    public bool UseWindowsAuth { get; set; } = true;
    public string UserID { get; set; } = "";
    public string Password { get; set; } = "";

    [XmlIgnore]
    public string ConnectionString
    {
        get
        {
            var parts = new List<string>
            {
                $"Data Source={Server}",
                $"Initial Catalog={Database}",
            };

            if (UseWindowsAuth)
            {
                parts.Add("Integrated Security=True");
            }
            else
            {
                parts.Add($"User ID={UserID}");
                parts.Add($"Password={Password}");
            }

            parts.Add("TrustServerCertificate=True");
            return string.Join(";", parts);
        }
    }

    public override string ToString() => Name;
}
