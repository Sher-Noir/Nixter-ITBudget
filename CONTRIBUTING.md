# Contributing to LedgerForge

LedgerForge welcomes bug fixes, documentation improvements, accessibility work, test coverage, and features that make organizational budgeting easier to operate safely.

## Development principles

- Keep LedgerForge organization-neutral. Do not add real organization names, employee identities, production account codes, internal network paths, source workbooks, or secrets.
- Preserve financial integrity. Server-side calculations and database constraints are authoritative; browser-calculated totals are never trusted as the system of record.
- Preserve history. Posted/approved records should be corrected through explicit workflows rather than silent deletion or mutation.
- Fail closed for authorization and sensitive document access.
- Prefer configuration and managed master data over source-code constants.
- Keep production assets locally hostable; do not introduce required public CDN dependencies.
- Maintain keyboard accessibility, semantic markup, visible focus states, and usable light/dark themes.

## Build and test

```powershell
dotnet restore LedgerForge.slnx
dotnet build LedgerForge.slnx --configuration Release --no-restore
dotnet test LedgerForge.slnx --configuration Release --no-build
```

Add or update tests for material behavior changes. Financial calculations, import validation, authorization, workflow state transitions, and irreversible operations require explicit regression coverage.

## Database changes

- Use EF Core migrations.
- Never rely on destructive schema changes running automatically at application startup.
- Explain data-conversion/backfill behavior in the pull request.
- Test migrations against a disposable SQL Server database before requesting merge.

## Import adapters

Import adapters must validate structure before reading financial data, preserve source lineage, hash source files, recalculate financial totals server-side, and expose exceptions before commit. Synthetic/public fixtures belong in tests; real organization migration files do not.

## Pull requests

Keep pull requests focused and document:

- the problem being solved,
- security/authorization impact,
- database/migration impact,
- financial-calculation impact,
- test coverage,
- deployment/configuration changes.

By contributing, you agree that your contribution is provided under the repository's MIT License.
