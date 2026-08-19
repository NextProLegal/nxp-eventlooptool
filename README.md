# Event Loop Tool

Desktop troubleshooting tool for detecting and repairing `EventsParent` loops in ProLaw databases.

ProLaw events can reference a parent event via the `EventsParent` column. When these references form cycles (A &rarr; B &rarr; A, or A &rarr; A), the Events Manager tree view breaks. This tool finds those cycles and lets an administrator clear the bad parent reference with one click.

## Requirements

- Windows 10/11
- .NET 8 SDK (for building) or the published single-file exe (no runtime needed)
- Network access to a ProLaw SQL Server instance
- Windows Authentication — your Windows login must exist in the ProLaw `Professionals` table
- ProLaw security class with **Admin = Y** and **MassChange = Y**

## Quick start

Download the latest `EventLoopTool.exe` from the [Releases](../../releases) page and run it. No install required.

To build from source:

```powershell
dotnet build src/EventLoopTool/EventLoopTool.csproj
dotnet run --project src/EventLoopTool/EventLoopTool.csproj
```

To publish a self-contained single-file exe:

```powershell
dotnet publish src/EventLoopTool/EventLoopTool.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish/
```

## How it works

1. **Connect** — Pick or create a connection profile (server + database). Profiles are saved to `%APPDATA%\NextPro\EventLoopTool\profiles.xml`.
2. **Authenticate** — The app looks up your Windows login in the `Professionals` table and checks your security class for Admin and MassChange permissions.
3. **Detect** — All events with a non-null `EventsParent` are fetched and run through a cycle-walk algorithm that identifies self-references, 2-node cycles, and n-node cycles.
4. **Review** — Loops are displayed as collapsible cards grouped by type (self / 2-node / n-node), with enrichment data: event number, parent number, matter ID, date, kind, event type, professional, and note.
5. **Repair** — Click "Clear parent" on any event to set its `EventsParent` to `NULL`, severing the loop.

The tool also supports printing a formatted HTML report and exporting results to CSV.

## Project structure

```
src/EventLoopTool/
├── Data/
│   ├── ProfileStore.cs        # XML read/write for connection profiles
│   └── ProLawRepository.cs    # All SQL queries + loop enrichment
├── Detection/
│   └── LoopDetector.cs        # Cycle-walk algorithm (ported from EventsManager TS)
├── Forms/
│   ├── MainForm.cs            # Primary UI — loop cards, repair, print, export
│   ├── ConnectForm.cs         # Connection profile picker
│   ├── EditProfileForm.cs     # Create/edit a connection profile
│   └── AccessDeniedForm.cs    # Shown when permissions are insufficient
├── Models/
│   ├── ConnectionProfile.cs   # Server name + connection string
│   ├── EventRecord.cs         # Raw event row from SQL
│   ├── Loop.cs                # Detected loop (type + node list)
│   ├── SecurityPermissions.cs # Admin + MassChange permission check
│   └── UserSession.cs         # Authenticated user state
└── Program.cs
sql/
├── detect-loops.sql           # Reference CTE for loop detection
└── test-data/
    └── event-loops.sql        # Sample data for testing
```

## Loop types

| Type | Description | Example |
|---|---|---|
| **Self** | Event references itself as its own parent | A &rarr; A |
| **2-node** | Two events reference each other | A &rarr; B &rarr; A |
| **N-node** | Three or more events form a cycle | A &rarr; B &rarr; C &rarr; A |

## Security model

The tool mirrors ProLaw's own security model:

- Authenticates via Windows login against the `Professionals` table
- Requires the user's security class to have both `Admin = 'Y'` and `MassChange = 'Y'`
- Registers a session in `CurrentProfessionals` (cleaned up on exit)
- The only write operation is `UPDATE Events SET EventsParent = NULL` on a single event, confirmed by dialog

## License

Internal tool — NextPro use only.
