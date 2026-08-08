# LedgerForge backup, restore, and upgrade runbook

LedgerForge stores authoritative financial records in SQL Server and document binaries in the configured non-web-root document directory. A recoverable deployment requires both data sets plus the effective deployment configuration.

## Backup scope

Back up all three of the following together:

1. **SQL Server database** — a native full backup of the configured LedgerForge database.
2. **Document storage** — the complete directory configured at `Documents:StoragePath`, including LedgerForge metadata sidecars.
3. **Deployment configuration** — `appsettings.Production.json` from the deployed web root and any host-level environment-variable overrides used for LedgerForge.

Do not rely on a copy of the application binaries as a database backup. Application binaries are reproducible from a tagged LedgerForge release; financial records and adopter configuration are not.

## Recommended schedule

For a production deployment, use the organization's established SQL Server and file-backup platform. A reasonable minimum is a daily full data backup with retention appropriate to the organization's financial-record policy. More frequent SQL log backups should be used when the database recovery model and recovery-point objective require them.

Backups must be stored separately from the LedgerForge application server. Encrypt backup media according to the organization's data-classification rules and periodically test restoration.

## Pre-upgrade backup

Before installing a newer LedgerForge release:

1. Confirm the current site is healthy at `/health`.
2. Record the installed LedgerForge release/tag and SHA-256.
3. Take and verify a native SQL Server backup.
4. Snapshot/copy the configured document directory.
5. Copy `appsettings.Production.json` and record any environment overrides.
6. Keep the current release installer/payload available until the upgrade is validated.
7. Review the new release notes and generated EF migration SQL.

LedgerForge migrations are forward, explicit deployment operations. The web application never applies production migrations automatically at startup.

## Upgrade sequence

1. Put the deployment into an approved maintenance window.
2. Stop the LedgerForge IIS site/application pool.
3. Preserve the pre-upgrade application directory or deploy to a versioned/staging directory.
4. Install the new application payload.
5. Preserve adopter-specific production configuration and non-web-root document storage.
6. Run `LedgerForge.Bootstrap initialize` using the production connection configuration. This applies only committed migrations and idempotent generic bootstrap data.
7. Run `LedgerForge.Bootstrap verify` and require zero pending migrations.
8. Start the IIS application pool/site.
9. Require `/health` HTTP 200.
10. Run the post-upgrade smoke checklist below before returning the site to normal use.

## Post-upgrade smoke checklist

Verify with appropriately permissioned accounts:

- Windows Authentication signs in successfully.
- Dashboard loads the current fiscal year without an exception.
- Work Center loads.
- Budget, Actuals, POs, Invoices, Vendors, Contracts, Renewals, Approvals, and Reports open.
- Administrator Diagnostics reports SQL connectivity, zero pending migrations, and writable non-web-root document storage.
- A harmless CSV export downloads successfully.
- A stored document can be authorized and streamed through LedgerForge.

Do not use a production financial transaction as an upgrade smoke test unless the organization's change procedure specifically calls for one.

## Restore procedure

A restore is an operational recovery, not an EF migration rollback.

1. Stop the LedgerForge IIS site/application pool.
2. Preserve the failed/current state for forensic review if required.
3. Restore the SQL Server database from the chosen recovery point.
4. Restore the matching document-storage snapshot.
5. Restore the matching production configuration.
6. Deploy the LedgerForge application release compatible with that database backup.
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
