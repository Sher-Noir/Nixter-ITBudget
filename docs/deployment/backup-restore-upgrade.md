# LedgerForge backup, restore, and upgrade runbook

LedgerForge stores authoritative financial records in SQL Server, document binaries and mutable host configuration in the configured non-web-root storage, and deployable application binaries under the installation directory. A recoverable deployment requires the data/configuration set, not a copy of replaceable application binaries.

## Backup scope

Back up all three of the following together:

1. **SQL Server database** — a native full backup of the configured LedgerForge database.
2. **Document and host storage** — the complete directory configured at `Documents:StoragePath`, including `.ledgerforge` mutable configuration/branding and document metadata.
3. **Deployment configuration** — `appsettings.Production.json` from the deployed web root plus any host-level environment-variable overrides used for LedgerForge.

Do not rely on a copy of the application binaries as a database backup. Application binaries are reproducible from a tagged LedgerForge release; financial records and adopter configuration are not.

## Recommended schedule

For a production deployment, use the organization's established SQL Server and file-backup platform. A reasonable minimum is a daily full data backup with retention appropriate to the organization's financial-record policy. More frequent SQL log backups should be used when the database recovery model and recovery-point objective require them.

Backups must be stored separately from the LedgerForge application server. Encrypt backup media according to the organization's data-classification rules and periodically test restoration.

## Pre-upgrade backup

Before installing a newer LedgerForge release:

1. Confirm the current site is healthy at `/health`.
2. Record the installed LedgerForge release/tag and SHA-256.
3. Take and verify a native SQL Server backup.
4. Snapshot/copy the configured document and `.ledgerforge` storage directory.
5. Copy `appsettings.Production.json` and record any environment overrides.
6. Keep the current release installer/payload available until the upgrade is validated.
7. Review the new release notes and generated EF migration SQL.

LedgerForge migrations are forward, explicit deployment operations. The web application never applies production migrations automatically at startup.

## Setup maintenance modes

When `LedgerForge.Setup.exe` detects an existing deployment it offers two maintenance choices:

- **Upgrade** — requires the Setup package to have a newer application version than the detected installed version. Upgrade stops IIS, cleanly replaces only the `Web` and `Bootstrap` payload directories, restores the existing production configuration exactly, applies committed forward migrations, preserves existing IIS bindings, restarts the site, and requires `/health` to pass.
- **Reinstall / repair** — performs the same safe payload replacement and verification but intentionally allows the same or an older Setup package. Use this to repair a damaged deployment or deliberately reinstall a known release. Reinstall is not a database reset.

Both modes preserve the existing SQL database, configured document storage, `.ledgerforge` mutable host data, production configuration, and existing security mappings. Maintenance mode does not create a new first-administrator grant.

A successful install or maintenance operation records **non-secret** installation metadata at `%ProgramData%\LedgerForge\install-state.json`. This lets later Setup packages identify custom install paths and show the installed/package versions before an upgrade. Existing deployments created before this metadata existed can still be discovered from the standard LedgerForge install path and production configuration.

## Upgrade sequence

1. Download `LedgerForge.Setup.exe` from the intended tagged GitHub Release and verify its published SHA-256/checksum policy.
2. Run Setup elevated on the LedgerForge server.
3. Confirm Setup detected the expected existing installation and installed version.
4. Select **Upgrade**.
5. Run the prerequisite check.
6. Start Upgrade. Setup stops the IIS site/application pool before replacing deployable files.
7. Setup preserves `appsettings.Production.json`, SQL/database state, documents, `.ledgerforge` host data, and current IIS bindings.
8. Setup runs `LedgerForge.Bootstrap initialize`, applying only committed forward migrations and idempotent generic bootstrap data.
9. Setup runs `LedgerForge.Bootstrap verify` and requires zero pending migrations.
10. Setup restarts IIS and requires `/health` HTTP 200 before reporting success.
11. Run the post-upgrade smoke checklist below before returning the site to normal use.

Do not delete or recreate the SQL database as part of a normal LedgerForge upgrade.

## Post-upgrade smoke checklist

Verify with appropriately permissioned accounts:

- the LedgerForge sign-in landing page renders and Windows sign-in succeeds;
- Dashboard loads the current fiscal year without an exception;
- Budget and Budget Item workspaces load, including actual entry;
- POs, Invoices, Vendors, Contracts, Renewals, Approvals, Reports, Imports, and Audit open according to configured permissions;
- Administration > Diagnostics reports SQL connectivity, zero pending migrations, and writable non-web-root storage;
- a harmless CSV export downloads successfully;
- a stored document can be authorized and streamed through LedgerForge;
- the top-bar update indicator is absent when the installed release is current.

Do not use a production financial transaction as an upgrade smoke test unless the organization's change procedure specifically calls for one.

## Restore procedure

A restore is an operational recovery, not an EF migration rollback.

1. Stop the LedgerForge IIS site/application pool.
2. Preserve the failed/current state for forensic review if required.
3. Restore the SQL Server database from the chosen recovery point.
4. Restore the matching document-storage and `.ledgerforge` snapshot.
5. Restore the matching production configuration.
6. Deploy the LedgerForge application release compatible with that database backup, using Reinstall / repair when appropriate.
7. Run `LedgerForge.Bootstrap verify`.
8. If the application release contains migrations newer than the restored database, either deploy the matching older application release or intentionally run the reviewed forward migrations; do not improvise manual schema edits.
9. Start IIS and require `/health` HTTP 200.
10. Re-run the smoke checklist and document the recovery point used.

## Migration rollback policy

LedgerForge does not automatically execute EF migration `Down` methods in production. Financial history can make destructive rollback unsafe even when a generated `Down` method exists. The preferred rollback is restoration of a known-good database/document/configuration backup together with the corresponding application release.

## Disaster-recovery test

At least periodically, restore a production-like backup set into an isolated SQL Server and document-storage location and verify:

- the database restores without error;
- LedgerForge Bootstrap reports zero unexpected pending migrations for the selected application release;
- document hashes/metadata remain valid;
- authorization remains fail-closed;
- core reports reconcile to the known backup point.

Record the restore duration and any manual steps so the recovery-time objective remains evidence-based.
