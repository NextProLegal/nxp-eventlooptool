# NXP-EventLoopTool

Desktop troubleshooting tool for detecting and repairing EventsParent loops in ProLaw databases.

## Shell Environment

NEVER use the Bash tool. ALWAYS use the PowerShell tool for all shell commands, including git operations.

## Unix Commands Don't Work in PowerShell

Never pipe to Unix commands in PowerShell. Use the PowerShell equivalents (`Select-Object -First N`, `Select-String`, `Where-Object`, `Measure-Object`, etc.).

## Git Commit Syntax

Multi-line git commit messages MUST use PowerShell here-strings (@' ... '@ syntax). The closing marker must be at column 0 with no leading whitespace.

## Build & Run

```powershell
# Build
dotnet build src/EventLoopTool/EventLoopTool.csproj

# Run (dev)
dotnet run --project src/EventLoopTool/EventLoopTool.csproj

# Publish single-file .exe (self-contained, no runtime needed on target)
dotnet publish src/EventLoopTool/EventLoopTool.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish/
```

## Architecture

- **Target:** .NET 8 WinForms (net8.0-windows)
- **Database:** Direct SQL via Microsoft.Data.SqlClient (Windows Auth)
- **No Fabric dependency** -- connects straight to ProLaw SQL Server

### Key directories

| Path | Purpose |
|---|---|
| `src/EventLoopTool/Models/` | POCOs: ConnectionProfile, EventRecord, Loop, UserSession, SecurityPermissions |
| `src/EventLoopTool/Data/` | ProfileStore (XML), ProLawRepository (SQL) |
| `src/EventLoopTool/Detection/` | LoopDetector -- cycle-walk algorithm ported from EventsManager TS |
| `src/EventLoopTool/Forms/` | WinForms: MainForm, ConnectForm, EditProfileForm, AccessDeniedForm |
| `sql/` | Reference SQL (CTE proposal, test data) |

### How it works

1. User picks a connection profile (stored in `%APPDATA%\NextPro\EventLoopTool\profiles.xml`)
2. App authenticates via Windows login against Professionals table
3. Checks SecurityClass: requires Admin = 'Y' AND MassChange = 'Y'
4. Registers session in CurrentProfessionals (cleaned up on exit)
5. Fetches events with non-null EventsParent, runs cycle-walk detection in C#
6. Displays loops grouped by type (self / 2-node / n-node) with enrichment data
7. "Clear parent" button per event to sever loops (UPDATE Events SET EventsParent = NULL)

### Detection algorithm

Ported from `NXP-EventsManager/app/src/components/tabs/EventLoopsTab.tsx:detectLoops()`.
Handles self-references, 2-node cycles, n-node cycles. Excludes rho tails.

## About Betty

Betty is NextPro's name for Claude Code. See the main EventsManager repo for full context.
