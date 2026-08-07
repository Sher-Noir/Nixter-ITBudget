# CRCH IT Budget Management System

Internal IT budget management system for Charles River Community Health (CRCH). This repository is being built as a secure ASP.NET Core MVC modular monolith for IIS, Windows Authentication, and SQL Server.

## Current status

Active implementation on the draft integration PR. The current foundation includes:

- .NET 10 LTS modular-monolith solution structure.
- SQL Server / EF Core models for fiscal years, fiscal periods, budget versions, budget items, allocations, managed lookups, import lineage, and AD authorization mappings.
- Integrated Windows Authentication with application-role policies that fail closed.
- Deployment-config and database-backed AD group mappings plus a protected administration page.
- Verified FY2027 workbook reader and persisted migration preview workflow.
- FY2027 reconciliation targets verified from the supplied workbooks: 68 items, $830,683.48 planned total, 44 Must Have.
- Allocation and migration workflow unit-test coverage.

Implementation status and remaining milestones are tracked in `docs/architecture/implementation-checklist.md`.

## Technology baseline

- .NET 10 LTS / ASP.NET Core MVC + Razor
- Microsoft SQL Server / Entity Framework Core
- IIS in-process hosting
- Integrated Windows Authentication
- Server-rendered UI with progressive enhancement
- ClosedXML for controlled Excel import/export workflows
- xUnit, integration tests, and Playwright UI tests

## Local build

```powershell
dotnet restore Crch.ItBudget.slnx
dotnet build Crch.ItBudget.slnx --configuration Release --no-restore
dotnet test Crch.ItBudget.slnx --configuration Release --no-build
```

Production deployment must use explicit database migration steps; destructive migrations must not run automatically at application startup.

## Security bootstrap

Checked-in configuration grants no AD group access. Configure an approved System Administrator bootstrap group through deployment configuration before first interactive use, then manage normal database-backed mappings in `/admin/security`. See `docs/deployment/ad-authentication.md`.

## FY2027 migration

The current migration preview treats `Raw Budget Info` as the authoritative 68-row source, recalculates planned totals from quantity × unit cost, preserves source item numbering, and records source SHA-256/row lineage. `Raw Budget Detail` is treated as a conflicting secondary source and is never silently merged.

The preview workflow stores audit/exception records only; transactional commit into authoritative budget records remains an open milestone.

## Security note

This system is not intended to store PHI or clinical records. Do not upload clinical data.
