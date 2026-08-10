# LedgerForge production deployment

This document defines the supported LedgerForge v1 deployment boundary. The Windows Setup application automates the LedgerForge application/database/IIS configuration after platform prerequisites are installed. It deliberately does not download privileged third-party/server prerequisites from the internet.

## Supported v1 topology

The reference deployment is a Windows Server or supported Windows workstation used as an evaluation host with:

- IIS and the Windows Authentication role service;
- the .NET 10 ASP.NET Core Hosting Bundle / ASP.NET Core Module V2;
- SQL Server or SQL Server Express reachable by Windows Integrated Security;
- LedgerForge running in its own IIS application pool;
- LedgerForge document/configuration storage on a local or organization-managed path outside the web root;
- Windows/Active Directory identities used for application authentication and authorization.

A single-server IIS + local SQL Server/Express layout is the fully automated Setup path. A remote SQL Server can be used when an administrator provisions the application identity/login and connection configuration according to organizational policy.

## Why prerequisites are not auto-downloaded

LedgerForge Setup fails closed when IIS, Windows Authentication, ASP.NET Core Module V2, or the chosen SQL Server is unavailable. It does not silently download or execute server software because doing so would make installation dependent on mutable internet endpoints and would bypass many organizations' patching/software-distribution controls.

Organizations may deploy the Microsoft prerequisites with their normal endpoint/server-management platform before running LedgerForge Setup. Offline installation is supported because LedgerForge itself is packaged as a self-contained Setup executable with an embedded application payload.

## TLS / HTTPS

The field-tested Setup path uses a localhost HTTP binding for initial validation. A production site must use HTTPS before it is exposed beyond an isolated evaluation host.

Recommended procedure:

1. Obtain a server-authentication certificate for the LedgerForge DNS name from the organization's certificate authority or approved public CA.
2. Import the certificate into the Local Computer certificate store.
3. Create an IIS HTTPS binding for the LedgerForge site using the approved DNS name and certificate.
4. Remove the localhost-only HTTP binding, or retain HTTP only to redirect to HTTPS according to organizational policy.
5. Set `Deployment:HttpsRedirection` to `true` in the production configuration.
6. Restart/recycle the LedgerForge application pool.
7. Verify `https://<ledgerforge-name>/health` returns HTTP 200 and the LedgerForge Windows sign-in flow works.

Example administrator PowerShell after a certificate is already installed:

```powershell
Import-Module WebAdministration
$site = 'LedgerForge'
$hostName = 'ledgerforge.example.org'
$thumbprint = '<certificate thumbprint>'
$httpsPort = 443

New-WebBinding -Name $site -Protocol https -Port $httpsPort -HostHeader $hostName -SslFlags 1
$bindingPath = "IIS:\SslBindings\0.0.0.0!$httpsPort!$hostName"
Get-Item "Cert:\LocalMachine\My\$thumbprint" | New-Item $bindingPath -Force
```

Certificate acquisition/private-key handling remains an infrastructure responsibility; LedgerForge does not generate or export private TLS keys.

## Windows Authentication and application access

LedgerForge uses an explicit application sign-in landing page instead of forcing a Windows challenge on the first request.

For the LedgerForge IIS application:

- **Windows Authentication: Enabled**
- **Anonymous Authentication: Enabled**

Anonymous IIS access is required only so the application can render `/account/login`, `/health`, and friendly error/status endpoints before a Windows challenge. It does **not** make protected LedgerForge routes anonymous; ASP.NET Core authorization still protects application data and actions.

When the user chooses **Continue with Windows**, `/account/windows` requires authenticated Windows access and Negotiate performs the challenge as needed. After authentication, LedgerForge resolves configurable Role → Module → Access Level grants plus any retained legacy bootstrap/recovery mappings.

The application fails closed when authorization configuration or directory membership cannot be resolved.

## Active Directory service identity

Normal Windows sign-in and token-based group checks do not require LedgerForge to store an Active Directory bind password.

If directory search/autocomplete is enabled in a future release, prefer a dedicated least-privilege gMSA as the IIS application-pool identity and use the process identity for directory reads. Do not store a reusable AD password in LedgerForge configuration, SQL Server, source control, the web root, or installer arguments. See `ad-authentication.md` for the detailed model.

## SQL Server permissions

The web application's IIS application-pool identity requires only the permissions needed by the application runtime. The field-tested local-server Setup creates the virtual-account login/user and grants `db_datareader` and `db_datawriter`.

If the application pool is changed to a domain/gMSA identity, provision the SQL login/user for that identity before switching the pool and re-run deployment diagnostics. Migration/bootstrap credentials are more privileged and are used only during explicit deployment. The normal web application does not call `Database.Migrate()` at startup.

## Filesystem permissions

The deployed web/application directory should be read/execute for the application-pool identity. The configured document-storage directory requires modify access for that identity and must not be beneath `wwwroot`.

Mutable organization settings, branding, and configurable role/module access data are stored beneath the protected `.ledgerforge` configuration root associated with document storage. Preserve this directory across upgrades along with SQL Server and document files.

Administration → Diagnostics verifies database access, migration state, and document-storage location/writability without revealing the connection-string value.

## Upgrade and recovery

Follow `backup-restore-upgrade.md` before every production upgrade. Back up SQL Server, document storage (including `.ledgerforge`), and production configuration together. LedgerForge favors restoring a known-good backup/application release over executing generated migration `Down` operations against financial history.

Do not wipe/recreate an existing LedgerForge database simply because a newly released UI control is not visible. First verify the installed web payload, workflow/artifact source SHA, IIS physical path, Setup replacement behavior, and app-pool/site restart. Browser cache should be investigated only after the server payload is confirmed current.

## Validation before go-live

Require all of the following:

- tagged LedgerForge release artifact checksum verified;
- current CI build/test and SQL migration gate green;
- `/health` HTTP 200 over HTTPS;
- `/account/login` renders without an automatic Windows challenge;
- **Continue with Windows** authenticates the expected domain identity;
- authorized and unauthorized application identities tested;
- Role → Module → Access Level boundaries verified on representative modules;
- Administrator Diagnostics all expected checks pass;
- document upload/download authorization verified;
- backup and restore procedure tested in an isolated environment;
- fiscal-year/account/dimension configuration reviewed by the adopting organization;
- no sample/evaluation financial data retained in the production database.