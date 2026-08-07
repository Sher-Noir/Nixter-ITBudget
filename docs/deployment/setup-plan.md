# LedgerForge setup plans

`LedgerForge.Setup.Engine` defines a versioned JSON setup-plan document for recording/reviewing adopter deployment choices without storing credentials.

A plan contains organization name, IIS site/application paths, document-storage path, SQL Server/database name, initial Windows administrator identity, optional administrator group, port, and host name. It intentionally contains no database password, certificate private key, API token, or other deployment secret.

The current schema version is `1`.

Example:

```json
{
  "SchemaVersion": 1,
  "Plan": {
    "OrganizationName": "Example Organization",
    "SiteName": "LedgerForge",
    "InstallPath": "C:\\Program Files\\LedgerForge",
    "DocumentsPath": "C:\\ProgramData\\LedgerForge\\Documents",
    "SqlServer": ".\\SQLEXPRESS",
    "DatabaseName": "LedgerForge",
    "InitialAdministratorIdentity": "EXAMPLE\\setup-admin",
    "SystemAdministratorGroup": "EXAMPLE\\LedgerForge Admins",
    "HttpPort": 8080,
    "HostName": "localhost"
  }
}
```

Plans are validated with the same `SetupPlanValidator` used by the installer configuration renderer. Unsupported schema versions fail rather than being interpreted heuristically.

A setup plan is configuration, not a credential store. Treat organization-specific plan files as deployment artifacts and keep them outside the public LedgerForge source repository when they contain private host names, directory identities, or infrastructure paths.
