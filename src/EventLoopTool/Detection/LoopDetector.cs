using EventLoopTool.Models;

namespace EventLoopTool.Detection;

/// <summary>
/// Detects EventsParent cycles. Port of the TypeScript algorithm from
/// NXP-EventsManager/app/src/components/tabs/EventLoopsTab.tsx:detectLoops().
/// </summary>
public static class LoopDetector
{
    public static List<Loop> DetectLoops(List<EventRecord> events)
    {
        var loops = new List<Loop>();
        var eventById = new Dictionary<string, EventRecord>(events.Count);
        var parentOf = new Dictionary<string, string>();
        var inLoop = new HashSet<string>();

        foreach (var e in events)
        {
            eventById[e.Events] = e;

            if (string.IsNullOrEmpty(e.EventsParent)) continue;

            if (e.Events == e.EventsParent)
            {
                loops.Add(new Loop { Type = LoopType.Self, Nodes = [e] });
                inLoop.Add(e.Events);
            }
            else
            {
                parentOf[e.Events] = e.EventsParent;
            }
        }

        var globalVisited = new HashSet<string>();

        foreach (var start in parentOf.Keys.ToList())
        {
            if (globalVisited.Contains(start)) continue;

            var path = new List<string>();
            var pathIndex = new Dictionary<string, int>();
            var node = start;

            while (node != null && parentOf.ContainsKey(node) && !globalVisited.Contains(node))
            {
                if (pathIndex.TryGetValue(node, out int cycleStart))
                {
                    var cycleIds = path.Skip(cycleStart).ToList();
                    if (!cycleIds.Any(id => inLoop.Contains(id)))
                    {
                        var nodes = cycleIds
                            .Where(id => eventById.ContainsKey(id))
                            .Select(id => eventById[id])
                            .ToList();
                        var type = nodes.Count == 2 ? LoopType.TwoNode : LoopType.NNode;
                        loops.Add(new Loop { Type = type, Nodes = nodes });
                        foreach (var id in cycleIds) inLoop.Add(id);
                    }
                    break;
                }
                pathIndex[node] = path.Count;
                path.Add(node);
                node = parentOf.TryGetValue(node, out var next) ? next : null;
            }

            foreach (var n in path) globalVisited.Add(n);
        }

        return loops;
    }

    public static string GetDisplayNo(EventRecord e)
    {
        if (e.EventKind == "O") return e.RTF ?? e.Events[..8];
        return e.EventsNo?.ToString() ?? e.Events[..8];
    }

    public static string BuildChainLabel(Loop loop)
    {
        var labels = loop.Nodes.Select(GetDisplayNo).ToList();
        if (loop.Type == LoopType.Self)
            return $"{labels[0]} \u2192 {labels[0]}";
        return string.Join(" \u2192 ", labels.Append(labels[0]));
    }
}
