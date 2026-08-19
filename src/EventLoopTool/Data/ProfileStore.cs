using System.Xml.Serialization;
using EventLoopTool.Models;

namespace EventLoopTool.Data;

public static class ProfileStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "NextPro", "EventLoopTool", "profiles.xml");

    public static List<ConnectionProfile> Load()
    {
        if (!File.Exists(FilePath))
            return new List<ConnectionProfile>();

        try
        {
            var serializer = new XmlSerializer(typeof(List<ConnectionProfile>));
            using var reader = new StreamReader(FilePath);
            return (List<ConnectionProfile>)serializer.Deserialize(reader)!;
        }
        catch
        {
            return new List<ConnectionProfile>();
        }
    }

    public static void Save(List<ConnectionProfile> profiles)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var serializer = new XmlSerializer(typeof(List<ConnectionProfile>));
        using var writer = new StreamWriter(FilePath);
        serializer.Serialize(writer, profiles);
    }
}
