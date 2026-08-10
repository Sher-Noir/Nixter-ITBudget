# LedgerForge

LedgerForge is a free and open-source budget management platform for organizations that need structured annual planning, actuals, purchase orders, invoices, renewals, approvals, audit history, and controlled exports without relying on spreadsheets as the system of record.

LedgerForge is designed for self-hosted deployments using ASP.NET Core, SQL Server, IIS, and Windows authentication by default. Organization identity, branding, fiscal-year labels, role/module authorization, import expectations, and operational settings are configurable so adopters can tailor the application without maintaining a private fork.

## Current status

LedgerForge is under active development. The current foundation includes:

- .NET 10 LTS modular-monolith solution structure.
- SQL Server / EF Core models for fiscal years, budget versions, budget items, allocations, actual transactions, managed lookups, import lineage, procurement, approvals, audit history, and legacy-compatible authorization data.
- Explicit LedgerForge sign-in landing with Integrated Windows Authentication / Negotiate for the authenticated session.
- Configurable **Role → Module → Access Level** authorization using `None`, `View`, `Edit`, `Manage`, and `Admin`, with assignments to Windows users or Active Directory security groups.
- Legacy fixed-role directory mappings retained as a bootstrap/recovery compatibility path for existing installations.
- Organization and appearance administration with protected host-local overrides outside the web root.
- Direct upload/replace/remove controls for organization logo, browser icon, vendor logos, and budget-item logos.
- Light, dark, and system theme support.
- Fiscal-year planning without fiscal periods in the active product workflow. Legacy period tables/columns remain only for non-destructive upgrade/history compatibility.
- Actual entry directly from the related Budget Item, with the full Actual Ledger retained as a compatibility/bulk workflow.
- Adapter-based spreadsheet migration infrastructure with optional reconciliation expectations.
- Allocation, import workflow, security/navigation, integration, and spreadsheet-reader test coverage.

Implementation status and remaining milestones are tracked in `docs/architecture/implementation-checklist.md`.

## Technology baseline

- .NET 10 LTS / ASP.NET Core MVC + Razor
- Microsoft SQL Server / Entity Framework Core
- IIS in-process hosting
- Integrated Windows Authentication / Negotiate by default
- Server-rendered UI with progressive enhancement
- ClosedXML for controlled Excel import/export workflows
- xUnit, integration tests, and Playwright UI tests

## Organization configuration

Deployment defaults live under `Branding` in `appsettings.json` or environment variables. System Administrators can override normal branding values from `/admin/organization`.

Mutable host configuration is stored under a protected `.ledgerforge` directory associated with the configured document-storage root. If no document-storage path is configured, the application uses its protected `App_Data` fallback. Mutable configuration and uploaded branding must remain outside `wwwroot` and should be preserved across application upgrades.

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

Administrators can upload an organization logo and browser icon directly. Uploaded branding overrides the advanced `LogoPath` / `IconPath` fallback. Do not commit adopter-specific branding, production identities, internal finance mappings, or mutable host configuration to the upstream project.

## Local build

```powershell
dotnet restore LedgerForge.slnx
dotnet build LedgerForge.slnx --configuration Release --no-restore
dotnet test LedgerForge.slnx --configuration Release --no-build
```

Production deployment must use explicit database migration steps; destructive migrations must not run automatically at application startup.

## Windows sign-in and authorization bootstrap

The reference IIS deployment enables both Windows Authentication and Anonymous Authentication. Anonymous IIS access allows LedgerForge to render `/account/login`, `/health`, and friendly status/error pages; protected application routes still require ASP.NET Core authorization. Choosing **Continue with Windows** enters the explicit Negotiate-authenticated flow.

Checked-in configuration grants no organization-specific directory group access. Configure an approved System Administrator recovery/bootstrap group before first interactive administration, then create normal configurable roles in `/admin/security`, assign users or Active Directory groups, and set module access levels.

LedgerForge does not store a Windows/Active Directory user password. If richer directory searches are enabled later, the preferred Windows deployment is a dedicated least-privilege gMSA/application-pool identity rather than a reusable AD password in application configuration. See `docs/deployment/ad-authentication.md`.

## Import adapters

LedgerForge imports are adapter-based. Organization-specific spreadsheet layouts belong in dedicated import adapters and must not define the core domain model. The included legacy-budget workbook adapter demonstrates strict sheet/header validation, source hashing, server-side total recalculation, lookup resolution, preview persistence, and configurable reconciliation expectations without embedding adopter-specific totals or identities.

## License

LedgerForge is licensed under the MIT License. See `LICENSE`.

## Public-repository hygiene

Avoid committing organization names, internal network paths, employee data, private documents, production secrets, proprietary account mappings, or real migration source files to the upstream repository.

## LedgerForge v1

See [`docs/architecture/v1-completion.md`](docs/architecture/v1-completion.md) for the current v1 contract and [`docs/GETTING_STARTED.md`](docs/GETTING_STARTED.md) for installation and first-run guidance.