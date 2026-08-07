# LedgerForge Project Handoff

_Last implementation checkpoint verified against `agent/initial-scaffold` on 2026-08-07 at commit `2c17aee39068ea69d86b0994a6bad0bf3a490e2e`. This handoff update is later; always verify the live branch head before editing._

This file is the authoritative continuation checkpoint for LedgerForge.

## Repository state

- Repository: `Sher-Noir/Nixter-ITBudget`
- Active branch: `agent/initial-scaffold`
- Draft PR: `#1` — LedgerForge open-source budget platform foundation
- PR base: `main`
- Target stack: .NET 10 LTS, ASP.NET Core MVC/Razor, EF Core 10, SQL Server, IIS, Integrated Windows Authentication
- SDK: .NET `10.0.302`
- EF CLI: `dotnet-ef` `10.0.0`

## Non-negotiable engineering rules

- LedgerForge is organization-neutral and intended for public/open-source release.
- Never commit real adopter names, employee identities, financial mappings/data, private workbook names, internal paths, credentials, secrets, or adopter-specific directory groups.
- Organization identity, branding, fiscal years, AD groups, accounts, departments, locations, import expectations, and similar values remain configuration/master data.
- SQL Server / EF Core is authoritative for financial system-of-record data.
- Financial amounts use `decimal(19,4)` unless a field is intentionally a rate/percentage.
- Browser-calculated totals are never authoritative.
- Preserve approved/posted history; do not implement destructive financial edits.
- Financial relationships fail safe and use restrictive/no-action deletion.
- Authorization fails closed.
- Keep the CSP strict.
- Sensitive documents remain outside the public web root.
- Production EF migrations remain explicit deployment steps; never auto-migrate on normal web startup.

# Validation gate — GREEN

- Release build succeeds with 0 warnings and 0 errors.
- Tests pass: 63 unit, 4 integration, 1 UI smoke (68 total).
- EF Core `InitialCreate` is committed and matches the current model.
- CI generates migration SQL, rejects destructive/cascade patterns, applies the migration to disposable SQL Server 2022, checks schema invariants, runs `LedgerForge.Bootstrap initialize|verify`, and reruns tests.
- Applied disposable schema has zero cascade-delete foreign keys.
- Financial amount precision is validated at `decimal(19,4)` with the intentional allocation-percentage exception at `decimal(9,4)`.

# Setup Preview — REAL WINDOWS INSTALL GREEN

A real Windows evaluation host has now completed the Setup Preview end-to-end successfully.

Verified on the physical/evaluation host:

- Administrator elevation.
- IIS detection.
- .NET 10 ASP.NET Core Hosting Bundle detection.
- Embedded deployment payload validation.
- `AspNetCoreModuleV2` IIS registration detection.
- IIS Windows Authentication availability.
- Payload extraction/copy under `C:\Program Files\LedgerForge`.
- Separate document storage under `C:\ProgramData\LedgerForge\Documents`.
- Deployment configuration generation.
- IIS application-pool filesystem ACLs.
- Explicit committed EF migration application.
- Generic workflow lookup initialization.
- Explicit first LedgerForge System Administrator provisioning.
- Idempotent rerun behavior for the administrator rule and migrations.
- IIS application-pool SQL login/user provisioning.
- Database initialization and verification.
- IIS site and app-pool configuration/start.
- IIS worker-process creation.
- `/health` returned ready successfully.

Two field defects were found and fixed during this install pass:

1. Setup previously hid IIS start failures and gave weak health diagnostics. It now requires the site/app pool to be Started and records HTTP/IIS diagnostics.
2. `web.config` duplicated Windows/anonymous-authentication settings that Setup already writes to IIS `applicationHost.config`. On the evaluation host those web-level authentication sections were locked, producing IIS `500.19`. Authentication settings were removed from `web.config`; Setup remains the single deployment authority for those IIS settings.

The preview intentionally uses a localhost HTTP binding and generated production configuration disables HTTPS redirection for this evaluation mode. Production deployment must add TLS and restore HTTPS redirect policy together.

# Current deployment status

The project is now installable for evaluation. It is not yet the final production installer.

Still needed for production deployment:

- HTTPS/TLS certificate and reviewed DNS/binding support.
- Automatic prerequisite installation/repair for IIS, Hosting Bundle, and optionally SQL Express.
- Remote SQL Server and managed/service identity support.
- Repeatable clean-host automated Windows installer smoke coverage where practical.
- Installer signing and release verification.
- Repair/upgrade/rollback/resume behavior.
- Unattended/offline deployment modes.
- Production backup/restore, upgrade, rollback, and operations runbooks.

# Product-readiness gaps — PRIORITY ORDER

## 1. Outstanding PO commitment accounting

Current dashboard/reporting can double-count an obligation after a PO-backed invoice posts because gross issued PO commitments remain while the invoice also appears in actuals.

Implement one authoritative calculation:

`Outstanding commitment = issued PO gross - posted linked PO invoice amounts`, floored at zero.

Reuse the same server-side calculation across dashboard, reporting, and commitment export. Add regression tests for partial invoice, fully invoiced PO, multiple invoices, over-invoice protection/display behavior, unlinked invoices, draft/non-posted invoices, and void/reversal behavior.

## 2. Contract-first renewals

Make `Contract`/`ContractRenewal` authoritative where present. Use budget-item renewal dates only as fallback, suppress duplicates, and make dashboard/calendar/export agree.

## 3. Unified approvals

Expand the approval queue/dashboard counts beyond budget items and purchase orders to include invoice and budget-amendment workflows. Keep authorization and state transitions fail closed.

## 4. Forecasting semantics and export

Define/persist the forecast baseline snapshot, use the latest published forecast where appropriate on dashboard/reporting, and add forecast CSV export with regression coverage.

## 5. Fiscal close and rollover

After financial semantics above are authoritative, implement explicit fiscal-year close, carry-forward, version creation, locks, and auditable rollover behavior.

## 6. Field usability / evaluation defects

Treat issues found while exercising the installed application as a dedicated stabilization queue. Fix broken navigation, confusing empty states, validation gaps, workflow dead ends, layout/responsive issues, and permissions surprises before calling the application release-candidate quality.

# Release path

1. Keep the build/test/migration gates green after every change.
2. Work through evaluation-host defects as they are reported.
3. Fix financial-semantic priorities in the order above.
4. Run a focused end-to-end workflow pass: configuration -> fiscal year -> budget -> approvals -> PO -> invoice -> actuals -> reports -> renewals -> forecast -> audit/documents.
5. Harden the installer for production deployment.
6. Perform final open-source/privacy/secrets scan.
7. Update README/install docs, version the release, merge the foundation PR when appropriate, and publish a signed release artifact.
