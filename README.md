# LedgerForge

LedgerForge is a free and open-source budget management platform for organizations that need structured annual planning, actuals, purchase orders, invoices, renewals, approvals, audit history, and controlled exports without relying on spreadsheets as the system of record.

The application is designed for self-hosted deployments using ASP.NET Core, SQL Server, IIS, and Integrated Windows Authentication by default. Branding, organization identity, fiscal-year labels, authorization mappings, and operational settings are configurable so adopters can tailor LedgerForge to their environment without modifying source code.

## Current status

Active implementation on the draft integration PR. The current foundation includes:

- .NET 10 LTS modular-monolith solution structure.
- SQL Server / EF Core models for fiscal years, fiscal periods, budget versions, budget items, allocations, managed lookups, import lineage, and AD authorization mappings.
- Integrated Windows Authentication with application-role policies that fail closed.
- Deployment-config and database-backed AD group mappings plus protected administration screens.
- Generic organization/product branding configuration.
- Light, dark, and system theme support.
- Spreadsheet migration infrastructure with a sample FY2027 workbook adapter retained as an optional import module.
- Allocation and migration workflow unit-test coverage.

Implementation status and remaining milestones are tracked in `docs/architecture/implementation-checklist.md`.

## Technology baseline

- .NET 10 LTS / ASP.NET Core MVC + Razor
- Microsoft SQL Server / Entity Framework Core
- IIS in-process hosting
- Integrated Windows Authentication by default
- Server-rendered UI with progressive enhancement
- ClosedXML for controlled Excel import/export workflows
- xUnit, integration tests, and Playwright UI tests

## Configuration

LedgerForge ships with generic defaults and no organization-specific identity. Configure `Branding` in application configuration or environment variables:

```json
{
  "Branding": {
    "ProductName": "LedgerForge",
    "OrganizationName": "Your Organization",
    "ApplicationTitle": "Budget Management",
    "LogoPath": "/images/ledgerforge-logo.svg",
    "IconPath": "/images/ledgerforge-icon.svg",
    "FooterText": "LedgerForge — free and open-source budget management.",
    "TimeZone": "America/New_York",
    "DefaultFiscalYearLabel": "Current FY",
    "DefaultTheme": "system"
  }
}
```

Organization-specific logos can be placed under `wwwroot/images` or served from another locally hosted path. Checked-in defaults should remain generic so forks can be published without exposing internal organization details.

## Local build

```powershell
dotnet restore LedgerForge.slnx
dotnet build LedgerForge.slnx --configuration Release --no-restore
dotnet test LedgerForge.slnx --configuration Release --no-build
```

Production deployment must use explicit database migration steps; destructive migrations must not run automatically at application startup.

## Security bootstrap

Checked-in configuration grants no AD group access. Configure an approved System Administrator bootstrap group through deployment configuration before first interactive use, then manage normal database-backed mappings in `/admin/security`. See `docs/deployment/ad-authentication.md`.

## Import adapters

LedgerForge imports are adapter-based. Organization-specific spreadsheet layouts should live in dedicated import adapters and must not define the core domain model. The current FY2027 adapter remains as a reference implementation for validated spreadsheet migration, but its source-specific field mappings are not part of the LedgerForge product identity.

## Open-source intent

LedgerForge is intended to be reusable by other organizations. Avoid committing organization names, internal network paths, real employee data, private documents, production secrets, or proprietary finance mappings to the public repository.
