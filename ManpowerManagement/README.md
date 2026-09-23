# Manpower Management

Internal ASP.NET Core 8 Razor Pages application for workshop manpower and leave reporting.

## Run locally

For normal use, open `../ManpowerManagement-Run/ManpowerManagement.exe`, leave its console window open, then visit `http://localhost:5257`. Keep the other files in `ManpowerManagement-Run` together with the executable; they contain the web assets and configuration. The `ManpowerManagement` folder is the source code used when making changes.

To rebuild the run folder after changing the source, run `dotnet publish ManpowerManagement/ManpowerManagement.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=None --output ManpowerManagement-Run` from the workspace root.

### Development

1. Update `ConnectionStrings:ManpowerDb` in `appsettings.json` if a different SQL Server database is required.
2. For normal development, run `dotnet restore` once, then start the app with `./run-dev.ps1` from this folder and open `http://localhost:5257`.
   `dotnet watch` detects saved changes to Razor, C#, CSS, and JavaScript files, then applies Hot Reload when possible or restarts the app automatically. Refresh the browser after the terminal reports that the update is ready; do not run the app again manually. Use `dotnet run` only when file watching is not needed.
3. The first start applies EF Core migrations and seeds workshop data and the initial Admin account: `admin` / `ChangeMe!123`. Change this password before production use.

### Database

- The app connects to the shared `TaMFGdb` database on `svr120a`, which already hosts tables for another system (`issue_entries`, `tickets`, `users`, etc.) in the `dbo` schema.
- All Manpower Management tables (Identity + domain tables) live in a dedicated `mp` schema (`AppDbContext.OnModelCreating` sets `HasDefaultSchema("mp")`) so they never collide with the existing tables.
- Schema is managed with EF Core Migrations (not `EnsureCreated`), since `EnsureCreated` only checks whether *any* table exists in the database and would otherwise skip creation because of the pre-existing `dbo` tables. To add a migration after model changes: `dotnet tool install --global dotnet-ef` (once), then `dotnet ef migrations add <Name>` from this folder.

### Current validation status

- `dotnet build` completes successfully.
- Verified end-to-end on 2026-09-18: TCP 1433 to `svr120a` is reachable, the TLS-encrypted connection succeeds, EF Core migrations apply cleanly to the `mp` schema, seed data (roles, Admin user, Workshops) is created, and the app serves `/Account/Login` with HTTP 200.

## Deployment

Deploy behind IIS on the corporate Intranet and enforce company-network access with IIS/network firewall rules. Set the SQL connection string through an IIS environment variable or production configuration; do not commit credentials.

## Excel imports

- Employee: `EmployeeId`, `FullName`, `Workshop`, `StartDate`, `EmploymentType`
- Calendar: `WorkDate`, `Workshop`, `IsWorkingDay`
