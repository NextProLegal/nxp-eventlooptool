using Microsoft.Data.SqlClient;
using EventLoopTool.Models;
using EventLoopTool.Detection;

namespace EventLoopTool.Data;

public class ProLawRepository : IDisposable
{
    private readonly SqlConnection _conn;

    public ProLawRepository(SqlConnection conn) => _conn = conn;

    // ── Security ─────────────────────────────────────────────────────────────

    public (string atom, string securityClassAtom, string profName)? LookupProfessional(string windowsLogin)
    {
        string?[] candidates =
        {
            windowsLogin,
            windowsLogin.Contains('\\') ? windowsLogin.Split('\\')[1] : null,
            windowsLogin.Contains('@')  ? windowsLogin.Split('@')[0]  : null
        };

        foreach (var login in candidates)
        {
            if (string.IsNullOrEmpty(login)) continue;

            using var cmd = new SqlCommand(
                "SELECT Professionals, SecurityClass, ProfName FROM Professionals " +
                "WHERE UserID = @uid AND IsActive = 'Y'", _conn);
            cmd.Parameters.AddWithValue("@uid", login);
            using var rdr = cmd.ExecuteReader();
            if (rdr.Read())
            {
                return (
                    rdr.GetString(0),
                    rdr.GetString(1),
                    rdr.IsDBNull(2) ? login : rdr.GetString(2)
                );
            }
        }

        return null;
    }

    public SecurityPermissions LoadPermissions(string securityClassAtom)
    {
        using var cmd = new SqlCommand(
            "SELECT MassChange, Admin FROM SecurityClass WHERE SecurityClass = @sc", _conn);
        cmd.Parameters.AddWithValue("@sc", securityClassAtom);
        using var rdr = cmd.ExecuteReader();
        if (rdr.Read())
        {
            var massChange = rdr.IsDBNull(0) ? null : rdr.GetString(0);
            var admin = rdr.IsDBNull(1) ? null : rdr.GetString(1);
            return SecurityPermissions.FromRecord(massChange, admin);
        }
        return new SecurityPermissions();
    }

    public void InsertCurrentProfessionals(string professionalsAtom)
    {
        using var cmd = new SqlCommand(
            "INSERT INTO CurrentProfessionals (CurrentProfessionals, HostId, Professionals) " +
            "VALUES (NEWID(), @@SPID, @prof)", _conn);
        cmd.Parameters.AddWithValue("@prof", professionalsAtom);
        cmd.ExecuteNonQuery();
    }

    public void DeleteCurrentProfessionals()
    {
        try
        {
            using var cmd = new SqlCommand(
                "DELETE FROM CurrentProfessionals WHERE HostId = @@SPID", _conn);
            cmd.ExecuteNonQuery();
        }
        catch { /* best-effort teardown */ }
    }

    // ── Loop detection ───────────────────────────────────────────────────────

    public List<DisplayLoop> LoadLoops()
    {
        var events = GetEventsWithParents();
        var loops = LoopDetector.DetectLoops(events);
        if (loops.Count == 0) return [];

        // Collect IDs for enrichment
        var loopEventIds = new HashSet<string>();
        var eventTypeIds = new HashSet<string>();
        var profSetIds = new HashSet<string>();
        foreach (var loop in loops)
            foreach (var node in loop.Nodes)
            {
                loopEventIds.Add(node.Events);
                if (!string.IsNullOrEmpty(node.EventTypes)) eventTypeIds.Add(node.EventTypes);
                if (!string.IsNullOrEmpty(node.ProfSet)) profSetIds.Add(node.ProfSet);
            }

        var matterMap = GetMatterIds(loopEventIds);
        var typeMap = GetEventTypeNames(eventTypeIds);
        var profMap = GetProfessionalNames(profSetIds);

        var eventById = events.ToDictionary(e => e.Events);
        var kindLabel = new Dictionary<string, string>
            { ["D"] = "Docket", ["O"] = "Document", ["N"] = "Note" };

        return loops.Select(loop =>
        {
            var rows = loop.Nodes.Select(e =>
            {
                var parentEvent = !string.IsNullOrEmpty(e.EventsParent)
                    && eventById.TryGetValue(e.EventsParent, out var pe) ? pe : null;
                return new DisplayRow
                {
                    EventId = e.Events,
                    ParentId = e.EventsParent,
                    EventNo = LoopDetector.GetDisplayNo(e),
                    ParentNo = parentEvent != null ? LoopDetector.GetDisplayNo(parentEvent) : null,
                    Matter = matterMap.TryGetValue(e.Events, out var matters)
                        ? string.Join(", ", matters) : "\u2014",
                    EventDate = e.EventDate,
                    Kind = kindLabel.GetValueOrDefault(e.EventKind ?? "", e.EventKind ?? "\u2014"),
                    EventType = !string.IsNullOrEmpty(e.EventTypes)
                        && typeMap.TryGetValue(e.EventTypes, out var desc) ? desc : "\u2014",
                    Professional = !string.IsNullOrEmpty(e.ProfSet)
                        && profMap.TryGetValue(e.ProfSet, out var names)
                        ? string.Join(", ", names) : "\u2014",
                    Note = e.ShortNote ?? "",
                };
            }).ToList();

            return new DisplayLoop
            {
                Type = loop.Type,
                ChainLabel = LoopDetector.BuildChainLabel(loop),
                Rows = rows,
            };
        }).ToList();
    }

    public void ClearEventParent(string eventId)
    {
        using var cmd = new SqlCommand(
            "UPDATE Events SET EventsParent = NULL WHERE Events = @id AND EventsParent IS NOT NULL",
            _conn);
        cmd.Parameters.AddWithValue("@id", eventId);
        cmd.ExecuteNonQuery();
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private List<EventRecord> GetEventsWithParents()
    {
        using var cmd = new SqlCommand(
            "SELECT Events, EventsParent, EventsNo, RTF, EventKind, " +
            "CONVERT(varchar, EventDate, 23) AS EventDate, EventTypes, ProfSet, ShortNote " +
            "FROM Events WHERE EventsParent IS NOT NULL", _conn);

        var list = new List<EventRecord>();
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(new EventRecord
            {
                Events = rdr.GetString(0),
                EventsParent = rdr.IsDBNull(1) ? null : rdr.GetString(1),
                EventsNo = rdr.IsDBNull(2) ? null : Convert.ToInt32(rdr[2]),
                RTF = rdr.IsDBNull(3) ? null : rdr.GetString(3),
                EventKind = rdr.IsDBNull(4) ? null : rdr.GetString(4),
                EventDate = rdr.IsDBNull(5) ? null : rdr.GetString(5),
                EventTypes = rdr.IsDBNull(6) ? null : rdr.GetString(6),
                ProfSet = rdr.IsDBNull(7) ? null : rdr.GetString(7),
                ShortNote = rdr.IsDBNull(8) ? null : rdr.GetString(8),
            });
        }
        return list;
    }

    private Dictionary<string, List<string>> GetMatterIds(IEnumerable<string> eventIds)
    {
        var ids = eventIds.ToList();
        if (ids.Count == 0) return new();

        var result = new Dictionary<string, List<string>>();
        foreach (var batch in Batch(ids, 900))
        {
            using var cmd = new SqlCommand();
            cmd.Connection = _conn;
            var inClause = BuildInClause(cmd, "eid", batch);
            cmd.CommandText =
                $"SELECT em.Events, m.MatterID FROM EventMatters em " +
                $"JOIN Matters m ON m.Matters = em.Matters " +
                $"WHERE em.Events IN ({inClause})";

            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                var eid = rdr.GetString(0);
                var mid = rdr.IsDBNull(1) ? null : rdr.GetString(1);
                if (mid == null) continue;
                if (!result.TryGetValue(eid, out var list))
                {
                    list = [];
                    result[eid] = list;
                }
                list.Add(mid);
            }
        }
        return result;
    }

    private Dictionary<string, string> GetEventTypeNames(IEnumerable<string> eventTypeIds)
    {
        var ids = eventTypeIds.ToList();
        if (ids.Count == 0) return new();

        var result = new Dictionary<string, string>();
        foreach (var batch in Batch(ids, 900))
        {
            using var cmd = new SqlCommand();
            cmd.Connection = _conn;
            var inClause = BuildInClause(cmd, "et", batch);
            cmd.CommandText =
                $"SELECT EventTypes, EventDesc FROM EventTypes WHERE EventTypes IN ({inClause})";

            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                if (!rdr.IsDBNull(1))
                    result[rdr.GetString(0)] = rdr.GetString(1);
            }
        }
        return result;
    }

    private Dictionary<string, List<string>> GetProfessionalNames(IEnumerable<string> profSetIds)
    {
        var ids = profSetIds.ToList();
        if (ids.Count == 0) return new();

        var result = new Dictionary<string, List<string>>();
        foreach (var batch in Batch(ids, 900))
        {
            using var cmd = new SqlCommand();
            cmd.Connection = _conn;
            var inClause = BuildInClause(cmd, "ps", batch);
            cmd.CommandText =
                $"SELECT ep.ProfSet, p.ProfName FROM EventProfs ep " +
                $"JOIN Professionals p ON p.Professionals = ep.Professionals " +
                $"WHERE ep.ProfSet IN ({inClause})";

            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                var ps = rdr.GetString(0);
                var name = rdr.IsDBNull(1) ? null : rdr.GetString(1);
                if (name == null) continue;
                if (!result.TryGetValue(ps, out var list))
                {
                    list = [];
                    result[ps] = list;
                }
                list.Add(name);
            }
        }
        return result;
    }

    private static string BuildInClause(SqlCommand cmd, string prefix, List<string> values)
    {
        var names = new string[values.Count];
        for (int i = 0; i < values.Count; i++)
        {
            names[i] = $"@{prefix}{i}";
            cmd.Parameters.AddWithValue(names[i], values[i]);
        }
        return string.Join(", ", names);
    }

    private static IEnumerable<List<string>> Batch(List<string> source, int size)
    {
        for (int i = 0; i < source.Count; i += size)
            yield return source.GetRange(i, Math.Min(size, source.Count - i));
    }

    public void Dispose() => _conn?.Dispose();
}
