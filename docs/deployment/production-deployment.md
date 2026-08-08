# LedgerForge production deployment

This document defines the supported LedgerForge v1 deployment boundary. The Windows Setup application automates the LedgerForge application/database/IIS configuration after platform prerequisites are installed. It deliberately does not download privileged third-party/server prerequisites from the internet.

## Supported v1 topology

The reference deployment is a Windows Server or supported Windows workstation used as an evaluation host with:

- IIS and the Windows Authentication role service;
- the .NET 10 ASP.NET Core Hosting Bundle / ASP.NET Core Module V2;
- SQL Server or SQL Server Express reachable by Windows Integrated Security;
- LedgerForge running in its own IIS application pool;
- LedgerForge document storage on a local or organization-managed path outside the web root;
- Windows/Active Directory identities used for application authorization.

A single-server IIS + local SQL Server/Express layout is the fully automated Setup path. A remote SQL Server can be used when an administrator provisions the application identity/login and connection configuration according to organizational policy, but remote service-account automation is outside the v1 interactive Setup path.

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
7. Verify `https://<ledgerforge-name>/health` returns HTTP 200 and normal Windows Authentication works.

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

## Windows Authentication and application roles

IIS anonymous authentication must be disabled and Windows Authentication enabled for the LedgerForge site. LedgerForge then maps authenticated identities to logical roles through configured directory groups and explicit audited grant/deny exceptions.

The application fails closed when role configuration or directory membership cannot be resolved. The first administrator may be explicitly provisioned by Setup and can subsequently configure directory groups from Administration > Security.

## SQL Server permissions

The web application's IIS application-pool identity requires only the permissions needed by the application runtime. The field-tested local-server Setup creates the virtual-account login/user and grants `db_datareader` and `db_datawriter`.

Migration/bootstrap credentials are more privileged and are used only during explicit deployment. The normal web application does not call `Database.Migrate()` at startup.

## Filesystem permissions

The deployed web/application directory should be read/execute for the application-pool identity. The configured document-storage directory requires modify access for that identity and must not be beneath `wwwroot`.

Administration > Diagnostics verifies database access, migration state, and document-storage location/writability without revealing the connection-string value.

## Upgrade and recovery

Follow `backup-restore-upgrade.md` before every production upgrade. Back up SQL Server, document storage, and production configuration together. LedgerForge favors restoring a known-good backup/application release over executing generated migration `Down` operations against financial history.

## Validation before go-live

Require all of the following:

- tagged LedgerForge release artifact checksum verified;
- current CI build/test and SQL migration gate green;
- `/health` HTTP 200 over HTTPS;
- Windows Authentication verified with both authorized and unauthorized test identities;
- Administrator Diagnostics all expected checks pass;
- document upload/download authorization verified;
- backup and restore procedure tested in an isolated environment;
- fiscal-year/account/dimension configuration reviewed by the adopting organization;
- no sample/evaluation financial data retained in the production database.
