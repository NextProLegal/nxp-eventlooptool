# Genesis

## Why this exists

The [Events Manager](https://github.com/NextProLegal/NXP-EventsManager) web app has a loop detection tab that finds circular `EventsParent` references in ProLaw databases. It works well — but it requires a Fabric workspace, a running web app, and a browser. When a consultant is on-site at a client SQL Server troubleshooting broken event trees, none of that infrastructure is available.

This tool exists so someone can carry a single `.exe` to any machine with network access to a ProLaw SQL Server, run it, see exactly which events are looping, and fix them on the spot.

## The problem it solves

ProLaw events can reference a parent event through the `EventsParent` column, forming a tree hierarchy. When these references form cycles — an event pointing to itself, two events pointing to each other, or longer chains that circle back — the tree view in ProLaw and the Events Manager breaks. These loops are data corruption: they shouldn't exist, but they do, and they need to be found and severed.

Before this tool, finding loops meant either:
- Running the Events Manager web app (requires Fabric + deployment)
- Manually inspecting data with SQL queries (slow, error-prone, no one remembers the right query)

## Key design decisions

**Standalone desktop app, not a web feature.** The whole point is bypassing Fabric and deployment infrastructure. A self-contained `.exe` that talks directly to SQL Server is the simplest thing that could work for on-site troubleshooting.

**Own repo, not a subfolder of EventsManager.** The algorithms are ported from TypeScript to C#, not shared. There's no code dependency between the two projects. Separate repo means a separate release cycle and no coupling to the web app's build pipeline.

**.NET 8 WinForms over .NET Framework 4.7.2.** The original design considered .NET Framework to avoid bundling a runtime. We went with .NET 8 + `PublishSingleFile` + `--self-contained` instead — the exe is larger (~60MB) but it's a single file with no install step and no runtime prerequisite on the target machine.

**C# port of the TypeScript algorithm, not the SQL CTE.** The design doc proposed using a recursive CTE for server-side detection. We went with Option B — porting the proven cycle-walk algorithm from `EventLoopsTab.tsx:detectLoops()` — because it was already tested against real data in the web app. The CTE was untested and added risk for no clear gain on the dataset sizes we deal with.

**Direct SQL via SqlClient, no ORM.** The queries are simple and few. An ORM would add weight for no benefit. Parameterized `SqlCommand` calls keep the data layer small and auditable.

**ProLaw's own security model, not a custom one.** The tool authenticates via Windows login against the `Professionals` table, then checks the user's security class for `Admin = 'Y'` AND `MassChange = 'Y'`. This mirrors how ProLaw itself gates access to bulk data changes. No separate user management to maintain.

**Minimal write surface.** The only mutation is `UPDATE Events SET EventsParent = NULL WHERE Events = @id` — clearing one parent reference on one event, confirmed by dialog. No batch operations, no cascading changes. The repair is as surgical as possible.

## Origin

Designed and built in August 2026. The design workorder was written in the EventsManager repo and copied here as `docs/design.md`. The loop detection algorithm, card layout, color scheme, print report, and CSV export are all ported from the EventsManager web app's Event Loops tab.
