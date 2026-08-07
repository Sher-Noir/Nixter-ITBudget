# LedgerForge Project Handoff

_Last implementation checkpoint verified against `agent/initial-scaffold` on 2026-08-07 at commit `c6e2a6efdf2d27d94ce5afe3f39545d68c76a080`. This handoff update itself is a later documentation-only commit, so always verify the live branch head before editing._

This file is the authoritative checkpoint for continuing LedgerForge. Do not rely on prior-chat memory. Read this file, verify the live GitHub branch/PR head, and inspect any commits newer than the checkpoint before changing code.

## Repository state

- Repository: `Sher-Noir/Nixter-ITBudget`
- Active branch: `agent/initial-scaffold`
- Draft PR: `#1` — LedgerForge open-source budget platform foundation
- PR base: `main`
- Verified implementation checkpoint: `c6e2a6efdf2d27d94ce5afe3f39545d68c76a080`
- Target stack: .NET 10 LTS, ASP.NET Core MVC/Razor, EF Core 10, SQL Server, IIS, Integrated Windows Authentication
- SDK pinned by `global.json`: .NET SDK `10.0.302`, `latestPatch`, prerelease disabled
- EF CLI pinned by `.config/dotnet-tools.json`: `dotnet-ef` `10.0.0`

At the beginning of the 2026-08-07 validation pass, the live branch was `cee02879fc957ff80bdb0e0fa2b247fcf7b0a1ca` while the prior handoff recorded `666b93562d3a329686a40e15db32ecef083d7b5f`. The intervening repository delta was documentation-only (`PROJECT_HANDOFF.md` and the Windows installer-wizard design), so application/schema understanding did not need to be rebased around hidden code changes.

## Non-negotiable engineering rules

- LedgerForge is organization-neutral and intended for public/open-source release.
- Never commit real organization names, employee identities, production account mappings, private workbook names/data, internal paths, credentials, secrets, or adopter-specific directory groups.
- Organization identity, branding, fiscal-year labels, AD groups, accounts, departments, locations, import expectations, and similar values are configuration/master data.
- SQL Server / EF Core is authoritative for financial system-of-record data.
- Financial amounts use `decimal(19,4)`.
- Browser-calculated totals are never authoritative.
- Preserve approved/posted history through amendments, reversals, workflow records, versioning, and immutable history rather than destructive edits.
- Financial relationships must fail safe and generally use restrictive/no-action deletion.
- Authorization fails closed.
- CSP remains strict; do not introduce inline scripts/styles casually.
- Sensitive documents remain outside the public web root and are streamed only through authorized endpoints.
- Production EF migrations are explicit deployment steps. Do not add automatic startup migrations.

# Validation gate — CURRENT STATUS

The validation/migration gate is **not green**. Do not continue to fiscal-year rollover or later feature priorities until the exact branch head can restore/build/test cleanly and the first migration is generated/reviewed/applied to disposable SQL Server.

## Build/test execution status

Required commands remain:

```powershell
dotnet restore LedgerForge.slnx
dotnet build LedgerForge.slnx --configuration Release --no-restore
dotnet test LedgerForge.slnx --configuration Release --no-build
```

What is known:

- `Directory.Build.props` enables nullable reference types, latest analysis, and `TreatWarningsAsErrors=true`.
- `global.json` now pins the intended .NET 10 SDK baseline to `10.0.302`.
- The current agent execution container has no .NET SDK and cannot obtain a usable SDK through its restricted network/package path. Debian package repositories cannot resolve, and the mediated downloader rejects the SDK archive/package formats.
- Therefore the required local commands have **not** been executed successfully in this checkpoint.
- No claim of compiler-clean, warning-clean, or test-clean status is valid yet.

## GitHub Actions status

Workflow: `.github/workflows/ci.yml`

Repository-side CI fixes completed during this validation pass:

- Removed invalid `actions/setup-dotnet` NuGet caching. The workflow had `cache: true` but the repository has no `packages.lock.json`; setup-dotnet documents that this configuration fails when no lock file exists.
- Added explicit `permissions: contents: read`.
- CI now consumes `global.json` rather than an unpinned `10.0.x` SDK range.
- Added `dotnet --info` before restore so future runnable jobs expose the exact SDK/runtime environment.
- The current build/test job uses `ubuntu-latest`. The current LedgerForge solution targets portable `net10.0`; the UI test project is also currently platform-neutral. When the future WPF Setup project (`net10.0-windows`) enters the solution, add an appropriate Windows build job or matrix rather than assuming the portable job validates the installer.

Current infrastructure blocker is confirmed across hosted runner OS choices:

- Windows-hosted run for implementation checkpoint `7bc31afd1a072ed64e23ea5350096f5fb49eaa35`: workflow run `31214601900`, job `92985055015`, completed/failure with `steps: null` and no downloadable job log.
- Ubuntu-hosted run for implementation checkpoint `c6e2a6efdf2d27d94ce5afe3f39545d68c76a080`: workflow run `31214853871`, job `92985875087`, completed/failure with `steps: null` and no downloadable job log.
- Because both Windows and Ubuntu jobs fail before GitHub reports any executable step, this is not a Windows-runner-specific application/SDK problem.
- The connected GitHub API does not expose repository/account Actions billing/runner-allocation diagnostics, so do not guess at billing, quota, policy, or runner causes without external evidence.
- Once GitHub actually creates steps, inspect the first real failing step and fix it.

# Persistence/model validation completed

The prior handoff overstated the completeness of `LedgerForgeDbContext`. Source modules existed for amendments, forecasts, contracts/renewals, and audit, but the context did not expose/configure those entities. Several controllers/services referenced missing DbSet properties, and the web host did not register multiple newer services or the audit interceptor.

## `LedgerForgeDbContext` corrections

Commit `553a474f09cab687a2862dd0c6613622ae2c3e4c` completed the EF model surface.

Added DbSets/configuration for:

- `BudgetAmendment`
- `ForecastScenario`
- `ForecastLine`
- `Contract`
- `ContractRenewal`
- `AuditEvent`

Key schema rules now encoded in the model:

- New financial amount columns explicitly use `decimal(19,4)`.
- New financial relationships use `DeleteBehavior.Restrict`.
- Amendment delta cannot be zero; resulting revised total cannot be negative.
- Forecast totals cannot be negative.
- Contract date range, annual amount, and renewal-notice-day constraints are explicit.
- Contract-renewal notice date cannot be after renewal date; expected amount cannot be negative.
- Contract computed `RenewalNoticeDate` is explicitly ignored by EF.
- Unique/index coverage was added for scenario names, scenario lines, contract numbers, renewal dates, state/date queries, and audit lookup paths.
- Audit-event field lengths now match domain validation.
- Auditable entities receive bounded `CreatedBy`/`ModifiedBy` and `rowversion` configuration centrally.

Existing financial model configuration was reviewed across:

- fiscal years/periods
- budget versions/items/allocations
- actual transactions/reversals
- vendors
- purchase orders/lines
- invoices/allocations
- managed lookups/finance accounts
- import batches/rows/exceptions
- directory mappings/user exceptions

No new cascade-delete path was deliberately introduced.

## Runtime wiring corrections

Commit `4abbdf25bfc400367067ac1e328ba1aa904efd08` corrected the web host wiring.

Added/connected:

- `IAuditRequestContext` -> `HttpAuditRequestContext`
- `AuditSaveChangesInterceptor`
- interceptor registration on `LedgerForgeDbContext`
- `BudgetAmendmentService`
- `ForecastService`
- `InvoiceQueryService`
- `InvoiceWorkflowService`
- `ContractService`
- `AddHttpContextAccessor()`

Strict CSP was preserved. No startup database migration was added.

## Persistence regression tests

`tests/LedgerForge.IntegrationTests/PersistenceModelTests.cs` was added and tightened in commits `934b58c26688abfb2305d6c03061f6784a343dc8` and `f8bef5c2e6349738ae06987b202b74d1bad4f466`.

The tests are intended to fail once executable CI resumes if:

- shipped financial/audit entity types disappear from the EF model,
- known financial amount properties drift away from precision 19 / scale 4,
- any modeled FK uses cascade/client-cascade delete.

These tests are source-reviewed but **not yet executed** because the validation runner remains blocked.

# First controlled EF migration — CURRENT STATUS

The first migration has **not** been generated, reviewed, committed, or applied. Do not hand-author a file and call it EF-generated.

Migration tooling is now deterministic:

- `.config/dotnet-tools.json` pins `dotnet-ef` to `10.0.0`, matching the EF Core package baseline in `LedgerForge.Infrastructure`.
- `global.json` pins the .NET SDK baseline.

Once a working .NET execution environment is available, run the validation commands first. Only after they are clean, use:

```powershell
dotnet tool restore

dotnet ef migrations add InitialCreate `
  --project src/LedgerForge.Infrastructure/LedgerForge.Infrastructure.csproj `
  --startup-project src/LedgerForge.Web/LedgerForge.Web.csproj `
  --context LedgerForgeDbContext `
  --output-dir Persistence/Migrations
```

Then generate and review SQL before applying it:

```powershell
dotnet ef migrations script 0 InitialCreate `
  --project src/LedgerForge.Infrastructure/LedgerForge.Infrastructure.csproj `
  --startup-project src/LedgerForge.Web/LedgerForge.Web.csproj `
  --context LedgerForgeDbContext `
  --output artifacts/InitialCreate.sql
```

Review the generated migration and SQL for at least:

- any `Drop*`, data-loss, or destructive operation
- accidental cascade deletes
- money/amount precision other than `decimal(19,4)` where not intentionally percentage/rate data
- missing max lengths
- nullable vs required relationship mistakes
- missing indexes and business-key uniqueness
- check-constraint correctness
- filtered-index correctness
- `rowversion` mappings
- TPC managed-lookup table behavior
- audit-table size/index choices
- SQL Server identifier/filtered-index compatibility

Then apply only to a disposable SQL Server database using a non-secret local/environment connection string and validate schema/application behavior there. Production deployment must continue to apply reviewed migrations explicitly; do not introduce `Database.Migrate()` at startup.

# Important stale claims discovered during validation

The previous handoff described several behaviors as implemented that are not present in the current authoritative source. Treat the following as defects/incomplete work, not completed features.

## Forecasting

Source present:

- forecast scenario/line domain types
- forecast service/controller/views
- scenario workflow tests

Not actually present yet:

- `ForecastLine` does not persist a separate baseline snapshot; it stores scenario id, budget item id, forecast total, and note.
- Dashboard does not consume the latest published forecast; it currently derives forecast as `Max(revised budget, committed + actual)`.
- Reporting/export service has no forecast export method/route.

## Commitment accounting

Source present:

- issued PO commitment calculation
- invoice posting creates actual-ledger rows
- optional invoice-to-PO relationship

Incorrect/incomplete today:

- Dashboard and Report Center sum gross issued PO line totals.
- Posted PO-backed invoice totals are not subtracted from outstanding commitment there.
- Therefore a posted invoice linked to an issued PO can currently increase Actual while the original PO amount remains fully Committed, causing double counting in available/forecast views.
- Commitment CSV currently exports gross issued PO lines rather than a reconciled outstanding commitment representation.

Do not call those totals production-authoritative until this is fixed and tested.

## Renewals/contracts

Source present:

- contract/contract-renewal domain and service/UI
- budget-item renewal calendar

Not actually present yet:

- Renewal calendar is still budget-item-renewal-date based; it is not contract-first.
- Dashboard renewal signals are budget-item based.
- Reporting renewal export is budget-item based and lacks the previously claimed contract source/vendor/action metadata.
- Duplicate suppression between contract and budget renewal sources is not implemented.

## Unified approvals

Current `ApprovalQueueService` includes:

- submitted budget items
- pending purchase orders

It does **not** currently include invoice approvals or budget-amendment approvals despite the previous handoff claiming a fully unified queue. Dashboard pending approvals likewise do not yet count all active workflow types.

These corrections to project status are important: source presence is not equivalent to verified end-to-end implementation.

# Module inventory — source present, validation still required

The branch contains substantial source/UI/domain coverage for:

- authentication/authorization and directory-role mapping
- organization/appearance settings
- managed lookup and finance administration
- fiscal years/periods/budget versions
- budget planning and allocations
- budget item approval workflow
- budget amendments
- forecasting/scenarios
- legacy spreadsheet import/review/commit
- actual transaction ledger and reversals
- vendors and purchase orders
- invoices and invoice posting
- contracts and renewal decision records
- dashboard
- report center and CSV exports
- central audit model/interceptor/UI
- physical document storage outside web root
- global search

None of the above should be promoted to "validated current head" until the Release build/tests run successfully and database-backed flows are exercised after the initial migration.

# Security/open-source checks to preserve

- Checked-in `appsettings.json` uses generic LedgerForge/"Your Organization" defaults and no adopter secrets.
- Checked-in AD group arrays are empty; authorization must continue to fail closed until deployment bootstrap is configured.
- Document content is stored under non-web-root `App_Data/Documents`.
- Document downloads are controller-mediated.
- CSP remains self-hosted/strict.
- No production startup migration is present.
- Real migration workbooks and adopter-specific configuration remain outside source control.

# Priority order after the gate

Do not skip Priority 1 or Priority 2 because later source already exists.

## Priority 1 — clean executable validation baseline

1. Obtain exact live `agent/initial-scaffold` checkout.
2. Verify `dotnet --version` resolves to the pinned .NET 10 SDK line.
3. `dotnet tool restore`.
4. Run restore/build/test commands exactly as documented above.
5. Fix every compiler warning/error; warnings are errors.
6. Re-run until clean.
7. Get GitHub Actions to execute real steps and confirm it matches local results.
8. Record exact command output/result and current implementation SHA here.

## Priority 2 — first controlled migration + disposable SQL Server validation

1. Generate `InitialCreate` from the complete corrected `LedgerForgeDbContext`.
2. Review migration and SQL.
3. Fix model issues and regenerate rather than editing around a wrong model where possible.
4. Apply to disposable SQL Server.
5. Run application/database integration smoke tests against it.
6. Validate migration history/schema/indexes/constraints.
7. Commit the reviewed migration and deployment notes.
8. Update this handoff with exact migration name/SHA and disposable-database result.

## Priority 3 — fiscal-year close and rollover

Still needed:

- close prerequisites/checklist
- period/fiscal-year closure orchestration
- controlled rollover wizard/service
- recurring/renewal copy-forward rules
- next-year budget version creation
- explicit handling for open POs, posted/unposted invoices, contracts, renewals, forecasts, and amendments
- immutable rollover audit history
- rollover regression tests

## Priority 4 — correctness gaps in existing procurement/reporting workflows

Before broadening features, repair and test the stale-claim defects recorded above:

- outstanding PO commitment reduced by posted linked invoices without going negative
- shared dashboard/reporting commitment semantics
- latest published forecast used where intended
- forecast export
- contract-first renewal calendar/report/dashboard behavior with fallback and deduplication
- invoice/amendment entries in unified approvals and pending counts
- purchase-order change orders/revisions with preserved history

## Priority 5 — import/report/admin hardening

Continue remaining work documented in architecture/checklist files, including:

- administrator-managed import profiles/schema definitions
- external actual import/reconciliation adapters
- stronger duplicate/business-key controls where required
- richer reporting/filtering/drill-down
- environment-backed authorization tests
- document-storage permission/runbook hardening
- backup/restore and operational runbooks

# Windows Setup wizard direction

Read `docs/deployment/windows-installer-wizard.md` before implementing installer work.

Intended product:

- `LedgerForge.Setup.exe`
- Windows WPF, self-contained `net10.0-windows`
- separate testable setup engine/core from the UI
- Express/local and Advanced/existing-infrastructure modes
- prerequisite detection before changes
- enable required IIS features
- install a LedgerForge-tested .NET Hosting Bundle
- optionally install a LedgerForge-tested SQL Server Express version
- verify downloaded hashes/signatures
- configure IIS site/app pool/bindings/Windows Authentication
- create/configure database and apply only reviewed EF migrations
- configure organization branding/settings and initial AD admin group
- configure non-web-root document storage and ACLs
- health checks, structured logs, resumability, and safe failure recovery
- later unattended install, offline bundle, repair, and upgrade modes

Installer implementation remains **blocked** on:

- clean build/test gate
- reviewed initial migration
- disposable SQL Server migration validation
- deterministic bootstrap/seed commands
- stable health-check endpoint
- supported/tested runtime/SQL version matrix

# Resume checklist

When continuing in a new session:

1. Read this file first.
2. Read `docs/deployment/windows-installer-wizard.md`.
3. Fetch PR #1 and live `agent/initial-scaffold` head.
4. Compare live head to the implementation checkpoint recorded at the top.
5. If newer commits exist, inspect them before editing.
6. Do not claim build/test/migration success unless exact current-head evidence exists.
7. Stay inside the validation/migration gate until it is green.
8. Keep this handoff updated at meaningful checkpoints.
