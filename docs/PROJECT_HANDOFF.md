# LedgerForge Project Handoff

_Last implementation checkpoint verified against `agent/initial-scaffold` on 2026-08-07 at commit `713562bc305e2217666b23f35f9808d00de871bb`. Later documentation-only commits may exist; always verify the live branch head before editing._

This file is the authoritative checkpoint for continuing LedgerForge. Do not rely on prior-chat memory. Read this file, verify the live GitHub branch/PR head, and inspect any commits newer than the checkpoint before changing code.

## Repository state

- Repository: `Sher-Noir/Nixter-ITBudget`
- Active branch: `agent/initial-scaffold`
- Draft PR: `#1` — LedgerForge open-source budget platform foundation
- PR base: `main`
- Verified implementation checkpoint: `713562bc305e2217666b23f35f9808d00de871bb`
- Target stack: .NET 10 LTS, ASP.NET Core MVC/Razor, EF Core 10, SQL Server, IIS, Integrated Windows Authentication
- SDK pinned by `global.json`: .NET SDK `10.0.302`, `latestPatch`, prerelease disabled
- EF CLI pinned by `.config/dotnet-tools.json`: `dotnet-ef` `10.0.0`

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

# Validation gate — GREEN

The restore/build/test/migration gate is green at the verified implementation checkpoint.

- Release build succeeds with 0 warnings and 0 errors.
- Tests pass: 63 unit, 4 integration, 1 UI smoke (68 total).
- EF Core `InitialCreate` is committed and matches the model.
- CI generates migration SQL, blocks destructive/cascade patterns, applies the migration to disposable SQL Server 2022, verifies schema invariants, runs `LedgerForge.Bootstrap initialize|verify`, and reruns tests.
- Applied disposable schema has zero cascade-delete foreign keys.
- Financial amount precision is validated at `decimal(19,4)` with the intentional budget-allocation percentage exception at `decimal(9,4)`.

# Setup Preview — FIRST REAL WINDOWS TEST

The Windows packaging workflow builds successfully and produces a self-contained `LedgerForge.Setup.exe` with an embedded web/bootstrap payload.

The first real on-machine preview install reached all of the following successfully:

- administrator elevation
- IIS detection
- Hosting Bundle detection after installation
- embedded payload validation
- IIS Windows Authentication detection
- payload extraction/copy to `C:\Program Files\LedgerForge`
- non-web-root document directory preparation under `C:\ProgramData\LedgerForge\Documents`
- deployment configuration write
- application-pool filesystem ACLs
- committed EF migrations
- generic workflow lookup initialization
- explicit first LedgerForge administrator provisioning
- IIS application-pool SQL login/user provisioning
- database initialization completion
- IIS site configuration
- bootstrap database verification

That first installer then timed out waiting for `/health`. This isolated the first field failure to the IIS-hosted web process/HTTP boundary rather than SQL migration/bootstrap.

## Fixes after first field test

Checkpoint `713562bc305e2217666b23f35f9808d00de871bb` addresses two setup defects and improves diagnostics:

1. Setup Preview's generated production configuration now explicitly sets `Deployment:HttpsRedirection=false`, matching the current localhost-only HTTP preview binding. `Program.cs` honors this setting while defaulting HTTPS redirection to enabled when not explicitly configured. Production TLS work remains separate and must restore an HTTPS binding plus redirect policy together.
2. Setup no longer silently ignores IIS application-pool/site start failures. It now queries and requires both objects to reach `state:Started`.
3. Preflight separately verifies that `AspNetCoreModuleV2` is actually registered with IIS, not merely that `aspnetcorev2.dll` exists.
4. If health still fails, Setup records the final HTTP status/reason/body plus IIS site, application-pool, worker-process, and ASP.NET Core Module registration diagnostics.

The replacement Windows packaging workflow for this checkpoint completed successfully. Artifact: `LedgerForge-Setup-Preview-win-x64`; executable SHA-256: `98365df9bc28286f8fb624c8d2e7d38bec65c2dadf6c1f4354496593c931609a`.

Re-running Setup over the first failed preview is supported for this path: migrations, generic seeds, initial administrator rule, and app-pool SQL principal provisioning are idempotent. Do not delete the database merely because the first health check failed.

# Deployment foundation

- `LedgerForge.Bootstrap initialize|verify` owns explicit deployment migration/bootstrap operations; normal web startup does not auto-migrate.
- `/health` returns ready only when the SQL database is reachable and there are no pending migrations.
- Setup Preview currently requires IIS + Windows Authentication, the .NET 10 ASP.NET Core Hosting Bundle, and an existing local SQL Server/SQL Express instance.
- Setup writes organization/connection/security configuration, configures IIS Windows Authentication, provisions initial admin access, and keeps document storage outside the web root.
- `docs/deployment/setup-preview.md` documents the preview prerequisites, re-run behavior, and known limitations.
- The production installer still needs automatic prerequisite handling, TLS/certificate support, remote SQL/service identities, clean-host automated smoke coverage, repair/upgrade/rollback/resume, unattended/offline modes, and signing.

# Important product-semantic gaps still open

These are not build/migration blockers, but they remain product-readiness work before production financial use.

## Commitment accounting

Dashboard/reporting currently use gross issued PO line totals. Posted PO-backed invoice amounts are not yet subtracted from outstanding commitments, so actual + committed can double count the same obligation. Implement one authoritative outstanding-commitment calculation and reuse it across dashboard/reporting/export.

## Renewals/contracts

Renewal views/signals/export are still primarily budget-item renewal-date based. Make contracts/contract-renewal records authoritative where present, with budget-item fallback and duplicate suppression.

## Unified approvals

`ApprovalQueueService` currently includes submitted budget items and pending purchase orders. Add invoice and budget-amendment approval work so dashboard/queue counts agree.

## Forecasting

`ForecastLine` does not persist a separate baseline snapshot. Dashboard does not yet consume the latest published forecast, and forecast CSV export is absent. Define the persistence and selection semantics, then add regression coverage.

## Fiscal close/rollover

Complete explicit fiscal-year close/carry-forward/version creation only after the above accounting semantics are authoritative and tested.

# Next actions

1. Run the replacement Setup Preview on the same evaluation Windows host and preserve the full Setup log.
2. If `/health` still fails, use the newly surfaced HTTP/IIS diagnostic detail to fix the exact runtime boundary rather than guessing.
3. Once the preview installs cleanly end-to-end, add automated or scripted clean-host IIS/SQL Express smoke validation where practical.
4. Continue product-semantic priorities: commitments, contract-first renewals, unified approvals, published forecasts/export, then fiscal rollover.
5. Continue the production installer roadmap only after the evaluation install path remains green.
