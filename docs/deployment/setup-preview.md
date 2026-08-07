# LedgerForge Setup Preview

LedgerForge Setup Preview is an early Windows installer intended for evaluation and development deployments while the production installer is being completed.

## What the preview installer does

- Runs elevated and validates its embedded deployment payload.
- Requires IIS with Windows Authentication enabled.
- Requires the .NET 10 ASP.NET Core Hosting Bundle / ASP.NET Core Module V2 to already be installed.
- Uses an existing **local** SQL Server or SQL Server Express instance through Windows Integrated Security.
- Copies the LedgerForge web application and bootstrap utility beneath the selected installation directory.
- Writes `appsettings.Production.json` from the values entered in Setup; no organization-specific production settings are shipped in the public payload.
- Creates a separate document-storage directory outside the public web root and grants the LedgerForge IIS application-pool identity access to it.
- Applies only the committed EF Core migrations through `LedgerForge.Bootstrap`; the web application does not auto-migrate at startup.
- Initializes generic workflow lookup values.
- Grants the selected first Windows user an explicit LedgerForge `SystemAdministrator` role entry. An optional AD group can also be configured for System Administrator access.
- Provisions the local IIS application-pool virtual account as a SQL login/database user with `db_datareader` and `db_datawriter` membership.
- Configures an IIS site with Windows Authentication and anonymous authentication disabled.
- Calls `/health` and requires both database connectivity and zero pending migrations before Setup reports success.

## Current prerequisites

Before running the preview executable on the target Windows server:

1. Install or enable IIS, including **Windows Authentication**.
2. Install the **.NET 10 ASP.NET Core Hosting Bundle** after IIS is enabled.
3. Install a local SQL Server edition. SQL Server Express is acceptable for evaluation.
4. Ensure the Windows account running Setup can create a database and Windows login on that SQL Server instance.
5. Run `LedgerForge.Setup.exe`. Its application manifest requests administrator elevation automatically.

The default SQL Server value is `.\SQLEXPRESS`, the default database is `LedgerForge`, and the preview site binds to `http://localhost:8080`. This localhost-only HTTP binding is for evaluation. A production deployment should use a reviewed DNS name and HTTPS certificate.

## Important preview limitations

This is **not yet the production-ready installer** described in `windows-installer-wizard.md`. The preview does not yet download/install IIS, the Hosting Bundle, or SQL Server Express; it does not configure a production TLS certificate; it does not yet support remote SQL Server/service-account setup; and repair/upgrade rollback is not yet transactional.

Use this package to validate LedgerForge installation and application behavior on a disposable or evaluation Windows server. Do not treat it as the final production deployment package yet.
