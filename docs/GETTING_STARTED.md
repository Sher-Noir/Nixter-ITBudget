# Getting started with LedgerForge

LedgerForge is a Windows/IIS + SQL Server budget, procurement, actuals, contract, renewal, approval, and reporting application intended to be configured for the adopting organization rather than forked with organization-specific source changes.

## 1. Prepare the Windows host

Install/enable:

- IIS;
- IIS Windows Authentication;
- the .NET 10 ASP.NET Core Hosting Bundle after IIS is enabled;
- SQL Server or SQL Server Express for the fully automated single-server Setup path.

Use an account that is local Administrator and can create the LedgerForge SQL database/login during installation.

See `deployment/production-deployment.md` for the production support boundary and HTTPS requirements.

## 2. Run LedgerForge Setup

Run the release `LedgerForge.Setup.exe` as Administrator. The installer checks elevation, IIS, ASP.NET Core Module V2, Windows Authentication support, and its embedded deployment payload.

Choose:

- organization display name;
- IIS site/application path;
- separate non-web-root document path;
- SQL Server/database;
- first Windows administrator identity;
- optional LedgerForge administrator directory group;
- HTTP validation port/host for the initial single-server deployment.

Setup writes adopter-specific production configuration, applies committed EF migrations through `LedgerForge.Bootstrap`, initializes generic workflow lookups, provisions the first LedgerForge administrator and IIS app-pool SQL access, configures IIS Windows Authentication, and requires `/health` to become ready.

The field-tested Setup path is idempotent for a re-run over the same installation/database. It does not recreate migrations or duplicate generic seeds/first-admin rules.

## 3. Configure HTTPS for production

Do not expose the initial HTTP validation binding to production users. Bind an approved certificate/DNS name in IIS and enable `Deployment:HttpsRedirection` as described in `deployment/production-deployment.md`.

## 4. Configure LedgerForge administration

Sign in as the first administrator and review:

- **Administration > Diagnostics** — SQL, migration, and document-storage state;
- **Administration > Organization** — adopter display/appearance settings;
- **Administration > Security** — directory groups and explicit role exceptions;
- **Administration > Finance** — finance/account structures;
- **Administration > Managed Lookups** — departments, locations, categories, need levels, frequency and other controlled values;
- **Fiscal Years** — create/confirm the current fiscal year and periods.

Authorization fails closed. Do not remove the last usable administrator mapping until another verified administrator path exists.

## 5. Load or create the budget

Use either:

- the Budget planning interface to create/edit planning items; or
- Imports to preview and reconcile a supported spreadsheet source before explicit acceptance/commit.

Imports preserve source hashes, row lineage, exceptions and administrator decisions. Preview is not authoritative financial posting; an accepted transactional commit is explicit.

## 6. Daily workflow

The common sequence is:

1. Plan/approve budget items and amendments.
2. Maintain vendors/contracts and upcoming renewals.
3. Create purchase orders and submit/approve/issue them.
4. Record PO receipts when goods/services are received.
5. Use change orders instead of editing issued PO history.
6. Create/approve/post invoices. Posted PO-backed invoices reduce the outstanding PO commitment and create actual-ledger spend.
7. Post non-invoice actuals manually or by transactional CSV import.
8. Use Work Center for approvals, renewals, import review, and fiscal-close work.
9. Use Report Center/exports for budget, actual, finance, commitment, renewal, and published-forecast data.

## 7. Forecasting

Create a forecast scenario for a fiscal year. LedgerForge snapshots each line's baseline when the scenario is created. Draft forecast values can change without changing that baseline. Publishing makes the scenario eligible as the authoritative latest-published dashboard/report forecast; archiving preserves history.

## 8. Fiscal close and rollover

LedgerForge refuses to close a fiscal year while required work remains unresolved. Close readiness checks open periods, approval/workflow state, invoices/amendments, and outstanding issued-PO commitments.

After close, rollover creates a new planning fiscal year/version with eligible planning baselines and allocations while leaving the closed year's approval and transaction history intact.

## 9. Backups and upgrades

Before every production upgrade, back up:

- the SQL Server database;
- configured document storage;
- production configuration/environment overrides.

Follow `deployment/backup-restore-upgrade.md`. LedgerForge does not automatically run destructive migration rollback operations against financial history.

## 10. Release integrity

Tagged releases include SHA-256 checksum files and a release manifest. Verify checksums before installation. Maintainers can optionally Authenticode-sign the Setup executable through repository signing secrets; unsigned community builds should still be checksum-verified.

## Evaluation vs production data

A successful installation proves the deployment path, not the adopting organization's financial controls. Before loading real data, validate role mappings, fiscal-year/account/dimension design, HTTPS, backups, restore testing, and the organization's approval/control procedures.
