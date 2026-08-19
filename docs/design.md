# Workorder: Event Loop Tool — Desktop .exe

**Objective:** Extract event loop detection, reporting, and repair into a standalone WinForms desktop application that runs directly on client SQL Servers for troubleshooting.

**Status:** Complete — repo created at `C:\repos\NXP-EventLoopTool`  
**Completed:** 2026-08-19

## Completion Summary

Built as a new repo (`NXP-EventLoopTool`), .NET 8 WinForms, targeting `net8.0-windows`. Compiles clean with zero warnings.

**What was built:**
- Connection infrastructure (ProfileStore, ConnectForm, EditProfileForm) from build doc pattern
- Windows auth + SecurityClass check (Admin + MassChange required)
- CurrentProfessionals session registration/teardown
- Loop detection: C# port of the TypeScript algorithm from EventLoopsTab.tsx
- MainForm with collapsible loop cards, type badges (orange/red/purple), chain labels
- "Clear parent" repair per event row with confirmation
- Print to HTML (opens in browser)
- Export to CSV (save dialog)
- SQL reference files: CTE proposal + test data script

**Publish command:**
```
dotnet publish src/EventLoopTool/EventLoopTool.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish/
```

---

## Why a desktop tool?

The Events Manager web app detects loops through the Fabric API, which requires a Fabric workspace and eval environment. A desktop .exe that talks directly to SQL Server can be carried to any client site, run by a consultant or admin, and used immediately — no Fabric, no browser, no deployment.

---

## What exists today (in the web app)

| Piece | Location | Language |
|---|---|---|
| Loop detection algorithm | `app/src/lib/analysis.ts:50-88` | TypeScript |
| Loop detection + grouping | `app/src/components/tabs/EventLoopsTab.tsx:51-97` | TypeScript |
| Display loop cards + tables | `EventLoopsTab.tsx:128-481` | React JSX |
| Repair (clear EventsParent) | `EventLoopsTab.tsx:242-254` | TS → Fabric PUT |
| Print/export to HTML | `EventLoopsTab.tsx:258-336` | TS/HTML |
| Chain label formatting | `EventLoopsTab.tsx:112-116` | TypeScript |
| SQL CTE (proposed, untested) | `docs/sql-proposals.md:32-93` | T-SQL |

---

## What the desktop .exe needs

### 1. Infrastructure (from the build doc)

Follow the pattern in `build-doc-db-connection-and-security.md`:

- [ ] **ConnectionProfile** model — Name + ConnectionString (no app-specific settings needed beyond that)
- [ ] **ProfileStore** — XML persistence in `%APPDATA%\NextPro\EventLoopTool\`
- [ ] **ConnectForm + EditProfileForm** — Profile selection dialog at startup
- [ ] **Security check** — LookupProfessional (Windows login → Professionals table), LoadPermissions, gate on appropriate SecurityClass flag (Admin or MassChange — TBD which flag is right for this tool)
- [ ] **CurrentProfessionals** session registration + teardown in FormClosing
- [ ] **UserSession** model

### 2. Loop detection — port or query?

Two viable approaches:

**Option A: SQL CTE (recommended)**
- Use the recursive CTE from `sql-proposals.md` directly as a SQL query
- Advantages: detection runs on the server, handles large Events tables efficiently, single round-trip
- The CTE is currently marked [UNTESTED] — this tool would be the forcing function to validate it
- Returns flat rows with a LoopType column; client groups them into loop objects

**Option B: Port the TypeScript algorithm to C#**
- Pull all Events rows (`SELECT Events, EventsParent, EventsNo, ShortNote, EventKind, RTF FROM Events WHERE EventsParent IS NOT NULL`), then run the cycle-walk in C#
- Same algorithm as `analysis.ts:countLoopNodes` / `EventLoopsTab.tsx:detectLoops`
- Advantage: proven, tested logic — just a language port
- Disadvantage: pulls potentially large dataset to the client

**Recommendation:** Start with Option A (SQL CTE). Fall back to Option B if the CTE has correctness issues on real data.

### 3. Data access layer

Simple repository class with direct `SqlCommand` calls (no ORM, no Fabric):

- [ ] `DetectLoops()` — execute the CTE, return `List<LoopRecord>`
- [ ] `GetLookupData()` — fetch Matters, EventMatters, EventTypes, EventProfs, Professionals for display enrichment (matter IDs, event type descriptions, professional names)
- [ ] `RepairLoop(Guid eventId)` — `UPDATE Events SET EventsParent = NULL WHERE Events = @id`
- [ ] `GetEventCount()` — total Events count for status bar

### 4. Main form UI

Port the visual design from EventLoopsTab, adapted for WinForms:

- [ ] **Summary bar** — loop count, event count, last-refreshed timestamp, Refresh button, Print button
- [ ] **Loop list** — scrollable panel of loop cards (or a grouped DataGridView / ListView)
  - Each card shows: type badge (self / 2-node / n-node), chain label, event count
  - Expandable to show detail table
- [ ] **Detail table per loop** — columns: Event/Document #, Parent #, Matter, Event Date, Kind, Event Type, Professional, Note, Action
- [ ] **Repair action** — "Clear parent" button per row → confirmation → SQL UPDATE → refresh
- [ ] **Color-coded type badges** — orange (self), red (2-node), purple (n-node) — same scheme as web app

### 5. Print / Export

- [ ] **Print** — generate HTML report (same structure as web app's `handlePrint()`), open in default browser, trigger print
- [ ] **Export to CSV** (nice-to-have) — flat dump of all loop events

### 6. EventClassFolder loops (stretch)

The project outline mentions EventClassFolder (EventTypes.NextLevel) loop detection as a secondary concern. Could add a second tab for this using the same pattern — same detection algorithm on the NextLevel FK chain.

---

## Tech stack

| Choice | Rationale |
|---|---|
| .NET Framework 4.7.2, WinForms | Matches AutoRecat pattern; runs on any client server without runtime install; single .exe via ILMerge or publish |
| System.Data.SqlClient | Built-in, no NuGet; Windows Auth (Integrated Security=True) |
| No Fabric dependency | Direct SQL — the whole point is bypassing Fabric for on-site use |

**Alternative:** .NET 8 WinForms with `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true` for a single .exe. Newer framework, but requires bundling the runtime (~60MB). Decision point for Auden.

---

## New repo or subfolder?

Options:
1. **New repo** (`NXP-EventLoopTool`) — clean separation, own release cycle
2. **Subfolder in this repo** (`tools/EventLoopTool/`) — loop detection logic stays close to the web app that inspired it

Leaning toward **new repo** since this is a standalone .exe with no shared code dependency on the web app. The algorithms are ported, not referenced.

---

## Rough build sequence

1. Scaffold WinForms project + connection infrastructure (from build doc)
2. Validate the SQL CTE against a real ProLaw database with known loops (use test-data script to seed them)
3. Build the repository class (detect, lookup, repair)
4. Build the main form — summary bar, loop list, detail tables
5. Wire repair action with confirmation
6. Add print/export
7. Test on a client server image
8. Package as single .exe

---

## Open questions

- [ ] Which SecurityClass flag gates access? Admin? MassChange? Or skip the permission gate entirely since this is a troubleshooting tool for consultants?
- [ ] .NET Framework 4.7.2 or .NET 8? (Framework = no runtime install; .NET 8 = modern but bigger .exe)
- [ ] New repo or subfolder?
- [ ] Should the CTE also report rho tails (events that chain into a loop but aren't in the cycle)? The web app excludes them — but for troubleshooting, seeing the full chain might be useful.
