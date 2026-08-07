# LedgerForge

LedgerForge is a free and open-source budget management platform for organizations that need structured annual planning, actuals, purchase orders, invoices, renewals, approvals, audit history, and controlled exports without relying on spreadsheets as the system of record.

LedgerForge is designed for self-hosted deployments using ASP.NET Core, SQL Server, IIS, and Integrated Windows Authentication by default. Organization identity, branding, fiscal-year labels, authorization mappings, import expectations, and operational settings are configurable so adopters can tailor the application without maintaining a private fork.

## Current status

LedgerForge is under active development. The current foundation includes:

- .NET 10 LTS modular-monolith solution structure.
- SQL Server / EF Core models for fiscal years, fiscal periods, budget versions, budget items, allocations, managed lookups, import lineage, and authorization mappings.
- Integrated Windows Authentication with application-role policies that fail closed.
- Deployment-config and database-backed directory-group mappings plus protected administration screens.
- Organization and appearance administration with host-local overrides under `App_Data`.
- Light, dark, and system theme support.
- Adapter-based spreadsheet migration infrastructure with optional reconciliation expectations.
- Allocation, import workflow, and spreadsheet-reader unit-test coverage.

Implementation status and remaining milestones are tracked in `docs/architecture/implementation-checklist.md`.

## Technology baseline

- .NET 10 LTS / ASP.NET Core MVC + Razor
- Microsoft SQL Server / Entity Framework Core
- IIS in-process hosting
- Integrated Windows Authentication by default
- Server-rendered UI with progressive enhancement
- ClosedXML for controlled Excel import/export workflows
- xUnit, integration tests, and Playwright UI tests

## Organization configuration

Deployment defaults live under `Branding` in `appsettings.json` or environment variables. System Administrators can override normal branding values from `/admin/organization`; those host-local overrides are stored in `src/LedgerForge.Web/App_Data/organization-settings.json` and are intentionally git-ignored.

```json
{
  "Branding": {
    "ProductName": "LedgerForge",
    "OrganizationName": "Your Organization",
    "ApplicationTitle": "Budget Management",
    "LogoPath": "",
    "IconPath": "",
    "FooterText": "LedgerForge — free and open-source budget management.",
    "TimeZone": "UTC",
    "DefaultFiscalYearLabel": "Current FY",
    "DefaultTheme": "system"
  }
}
```

Organization-specific logos should be placed under the deployment's locally hosted static assets and referenced by `LogoPath` / `IconPath`. Do not commit adopter-specific branding, production identities, or internal finance mappings to the upstream project.

## Local build

```powershell
dotnet restore LedgerForge.slnx
dotnet build LedgerForge.slnx --configuration Release --no-restore
dotnet test LedgerForge.slnx --configuration Release --no-build
```

Production deployment must use explicit database migration steps; destructive migrations must not run automatically at application startup.

## Security bootstrap

Checked-in configuration grants no directory group access. Configure an approved System Administrator bootstrap group through deployment configuration before first interactive use, then manage normal database-backed mappings in `/admin/security`. See `docs/deployment/ad-authentication.md`.

## Import adapters

LedgerForge imports are adapter-based. Organization-specific spreadsheet layouts belong in dedicated import adapters and must not define the core domain model. The included legacy-budget workbook adapter demonstrates strict sheet/header validation, source hashing, server-side total recalculation, lookup resolution, preview persistence, and configurable reconciliation expectations without embedding adopter-specific totals or identities.

## License

LedgerForge is licensed under the MIT License. See `LICENSE`.

## Public-repository hygiene

Avoid committing organization names, internal network paths, employee data, private documents, production secrets, proprietary account mappings, or real migration source files to the upstream repository.


## LedgerForge v1

See [`docs/architecture/v1-completion.md`](docs/architecture/v1-completion.md) for the current v1 contract and [`docs/GETTING_STARTED.md`](docs/GETTING_STARTED.md) for installation and first-run guidance.
