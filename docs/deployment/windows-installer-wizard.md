# LedgerForge Guided Windows Installer

Status: Planned deployment milestone

This document defines the intended **LedgerForge Setup** experience for organizations that want a low-touch Windows Server installation instead of manually configuring IIS, SQL Server, the .NET Hosting Bundle, the application, and initial LedgerForge settings.

The target deliverable is a signed, elevated executable named approximately:

`LedgerForge.Setup.exe`

The setup program should be usable by an administrator who has a newly provisioned Windows Server and wants LedgerForge running with minimal manual work.

## Product goal

Provide two supported deployment paths from the same installer:

1. **Express / Local Server** — install required Windows features, install a supported SQL Server Express instance when SQL Server is not already available, install the required .NET Hosting Bundle, deploy LedgerForge to IIS, create the database, apply reviewed EF migrations, configure permissions, and launch LedgerForge.
2. **Advanced / Existing Infrastructure** — use an existing SQL Server instance, existing certificates, custom service identity, custom IIS site/app-pool values, custom storage locations, and organization-specific directory mappings.

The installer must remain optional. Experienced administrators must still be able to deploy LedgerForge manually using documented deployment steps.

## Recommended implementation

### Setup executable

Build a dedicated Windows project, for example:

- `src/LedgerForge.Setup/LedgerForge.Setup.csproj`
- target: `net10.0-windows`
- UI: WPF
- publish: `win-x64`, self-contained, single-file where practical
- requested execution level: Administrator

WPF is preferred over a Windows App SDK dependency for the server installer because it is mature, available on supported Windows Server editions, and can be published self-contained without requiring the server to have the .NET runtime before setup starts.

The UI should be a thin shell over a testable installer engine. Installation operations must not live directly in button-click handlers.

Suggested internal structure:

- `LedgerForge.Setup.Core` — plans, state, prerequisite detection, download verification, install orchestration
- `LedgerForge.Setup` — WPF wizard UI
- `LedgerForge.Setup.Tests` — plan/preflight/unit tests

## Wizard flow

### 1. Welcome and safety

Display:

- LedgerForge version being installed
- open-source license
- supported Windows Server versions
- notice that administrative privileges are required
- notice that setup may require a restart
- link to installation log location

### 2. Preflight scan

Detect and show pass/warning/fail status for:

- 64-bit supported Windows Server
- administrator elevation
- pending reboot
- available disk space
- server hostname
- domain/workgroup membership
- IIS state and required role services
- .NET Hosting Bundle state
- installed SQL Server instances/editions/versions
- HTTP/HTTPS port conflicts
- requested installation and document-storage path permissions
- outbound HTTPS access for online prerequisite mode
- TLS certificate availability when HTTPS is selected

Do not make destructive changes during preflight.

### 3. Installation mode

Offer:

**Recommended**

- Local SQL Server Express
- local IIS site
- Windows Authentication
- local document-storage directory outside the web root
- installer-managed database creation and migrations

**Advanced**

- existing/local/remote SQL Server
- custom instance/database name
- custom IIS site and app pool
- custom binding/hostname/port/certificate
- custom application-pool identity or supported managed service account
- custom document-storage path

### 4. Organization configuration

Ask for normal LedgerForge settings:

- organization name
- application title
- product display name override if desired
- display timezone
- fiscal-year label/default behavior
- default light/dark/system theme
- optional local logo/icon paths
- support/contact text

These values should be written through the same configuration model used by the normal LedgerForge administration UI.

### 5. Authentication and first administrator

Default deployment remains Integrated Windows Authentication.

Ask for one of:

- Active Directory group to bootstrap as `SystemAdministrator`, or
- current authenticated domain administrator as an explicit temporary first-admin grant where supported.

The wizard must explain that directory authorization fails closed and that the selected group/identity must be resolvable before normal interactive use.

Do not request or store a user's domain password.

### 6. Database configuration

#### Recommended SQL Server Express mode

If no suitable SQL Server is selected, offer installation of the currently supported LedgerForge-pinned SQL Server Express release.

Important design rule: **do not blindly download whatever Microsoft labels "latest" at runtime**. LedgerForge releases should publish a signed prerequisite manifest that pins a tested SQL Server Express major/build family and approved Microsoft download location. Setup may offer a newer supported release only after LedgerForge compatibility has been validated.

For SQL Server 2025 Express, Microsoft documents a 50 GB maximum relational database size and command-line unattended installation support. SQL Server command-line setup supports quiet/basic modes and `IACCEPTSQLSERVERLICENSETERMS`; the LedgerForge wizard must surface Microsoft's license before initiating unattended setup.

Suggested local instance name:

`LEDGERFORGE`

Recommended installation should include only the Database Engine features LedgerForge requires.

The installer should prefer Windows authentication and should not create an application SQL login/password unless the administrator explicitly chooses SQL authentication.

#### Existing SQL Server mode

Collect and test:

- server/instance
- database name
- Windows vs SQL authentication
- encryption/trust settings
- migration/admin connection identity
- application runtime identity

Perform a connection test before leaving the page.

### 7. IIS configuration

On Windows Server, enable only required IIS role services instead of installing every IIS subfeature.

Expected minimum includes the appropriate equivalents of:

- Web Server (IIS)
- Management Console
- Windows Authentication
- Static Content
- Default Document
- HTTP Errors
- HTTP Logging
- Request Filtering
- Static Compression
- required WAS/process-model components

Use supported ServerManager/PowerShell or DISM APIs and require elevation.

The .NET Hosting Bundle must be installed **after IIS is enabled**. Microsoft documents that if the Hosting Bundle was installed before IIS, the Hosting Bundle installation must be repaired/re-run afterward.

Create:

- application directory, e.g. `C:\Program Files\LedgerForge\Web`
- non-web-root data directory, e.g. `C:\ProgramData\LedgerForge`
- document storage, e.g. `C:\ProgramData\LedgerForge\Documents`
- IIS application pool, e.g. `LedgerForge`
- IIS site, e.g. `LedgerForge`

Recommended ASP.NET Core app-pool setting: No Managed Code / Integrated pipeline.

Disable Anonymous Authentication and enable Windows Authentication for the LedgerForge site in the reference deployment.

### 8. Application identity and filesystem/database permissions

The installer should offer:

- default `ApplicationPoolIdentity`
- advanced custom domain service identity / supported gMSA configuration

Grant only required filesystem permissions to:

- LedgerForge app directory
- `App_Data`/configuration area as required
- document-storage root
- logs/temp paths

For local SQL Server, create the application database user for the selected application-pool/service identity with runtime DML permissions needed by LedgerForge. Migration/DDL authority should remain a setup/deployment concern and should not be required by the normal runtime identity.

### 9. Deploy LedgerForge

The setup release should contain or securely acquire a pre-published LedgerForge web deployment payload.

Actions:

1. stop/suspend the LedgerForge app pool when upgrading
2. snapshot configuration relevant to rollback
3. deploy versioned application files
4. write deployment configuration
5. protect secrets using appropriate Windows/server mechanisms rather than plaintext source-controlled files
6. create/update IIS site and bindings

### 10. Database creation and migrations

The installer must use LedgerForge's reviewed EF migration set.

For new installation:

- create database if absent
- execute all migrations in order
- initialize generic lookup/workflow values
- bootstrap the configured administrator group mapping

For upgrade:

- detect installed LedgerForge schema/application version
- back up database/configuration when the administrator enables the built-in backup step
- show pending migration summary
- apply forward migrations
- fail without silently destroying data if migration validation fails

Never run arbitrary destructive schema replacement or `EnsureCreated` against a production installation.

### 11. Configuration summary

Before committing changes, show a review page containing:

- IIS site/binding
- app-pool identity
- SQL server/instance/database
- authentication mode
- organization name/timezone
- bootstrap administrator mapping
- application/document/log paths
- prerequisites that will be downloaded/installed
- restart requirement

Provide **Back**, **Export Plan**, and **Install** buttons.

`Export Plan` should create a JSON file that can later be used for unattended/repeatable installation.

### 12. Installation execution

Display step-by-step status:

- Creating restore/rollback point metadata
- Enabling IIS
- Installing/repairing Hosting Bundle
- Installing SQL Server Express when selected
- Creating database/runtime identity
- Applying migrations
- Deploying application
- Configuring IIS/authentication
- Initializing LedgerForge
- Running health checks

Every step must write a timestamped structured log under a predictable location such as:

`C:\ProgramData\LedgerForge\Setup\Logs`

### 13. Health validation

Before declaring success, validate at minimum:

- IIS site started
- application pool healthy
- local HTTP/HTTPS request reaches LedgerForge health/startup endpoint
- database connection succeeds
- expected migration/schema version is present
- document directory is writable by application identity
- Windows Authentication is enabled / Anonymous disabled for reference configuration
- bootstrap administrator mapping exists

Provide direct links/buttons to:

- Open LedgerForge
- Open installation logs
- Open deployment/configuration folder

## Online and offline installers

LedgerForge should ultimately publish two release formats.

### Online bootstrapper

Small `LedgerForge.Setup.exe` that downloads pinned Microsoft prerequisites and the matching LedgerForge deployment payload.

Advantages:

- small download
- current tested prerequisite patches can be selected by manifest

### Offline bundle

Larger ZIP/ISO-style package containing:

- setup executable
- LedgerForge deployment payload
- pinned .NET Hosting Bundle installer
- pinned SQL Server Express installer/media where redistribution terms permit
- signed prerequisite manifest

This is important for disconnected/server networks.

## Download and supply-chain security

The installer will run as Administrator, so prerequisite handling must be treated as a high-trust supply-chain path.

Requirements:

- HTTPS only
- download only from allow-listed Microsoft/LedgerForge release locations
- pin supported versions in a LedgerForge release manifest
- verify SHA-256 where LedgerForge publishes an expected digest
- verify Authenticode signatures for Microsoft executables before execution
- verify LedgerForge setup/payload signatures when code-signing is available
- never execute a downloaded file based only on filename or HTTP success
- record downloaded version/hash/signer in setup logs
- keep prerequisite installers in a temporary protected directory and remove them after successful install unless the user selects caching

## Reboots and resumability

Windows feature installation or prerequisite servicing can require restart.

Setup should maintain a protected resumable state file under `ProgramData` and support:

1. save completed step/checkpoint
2. schedule/setup RunOnce or a supported resume mechanism
3. reboot
4. resume the wizard at the next safe step

Every installation action should be idempotent or have an explicit detection/check phase so setup can safely resume.

## Unattended installation

After the GUI stabilizes, support:

```text
LedgerForge.Setup.exe /quiet /config C:\Deploy\ledgerforge-install.json
```

The unattended configuration file must not contain plaintext passwords. Secrets should be supplied through Windows credential mechanisms, environment injection from the deployment system, or interactive secure input when unavoidable.

The same setup engine should power GUI and unattended modes to prevent configuration drift.

## Upgrade experience

A future `LedgerForge.Setup.exe` should also support an existing installation:

- Detect current LedgerForge version
- Check GitHub/LedgerForge release manifest for a newer stable version when online checks are enabled
- Show release/migration notes
- Back up configuration/database where configured
- Stop app pool
- Deploy new files
- Apply reviewed migrations
- Restart
- Run health checks
- Roll back application files/configuration if deployment fails before an irreversible database transition

Database rollback must be backup/restore based for irreversible production migrations rather than trusting generated migration `Down()` methods blindly.

## Setup should not do these things

- Do not expose the SQL Server instance to the public internet automatically.
- Do not open inbound firewall ports for remote SQL unless explicitly requested and warned.
- Do not create weak SA passwords or enable SQL authentication by default.
- Do not ask for or store Active Directory user passwords.
- Do not install unrelated IIS features.
- Do not silently disable TLS/certificate validation.
- Do not execute unsigned/unverified downloaded prerequisite installers.
- Do not run destructive migrations automatically at app startup.
- Do not overwrite an existing LedgerForge deployment without detecting/versioning/backing up the installation first.

## Proposed setup project milestones

### Setup M1 — engine/preflight

- Create `LedgerForge.Setup.Core` and tests.
- Detect administrator state, OS, IIS, Hosting Bundle, SQL instances, ports, disk, pending reboot.
- Define installation plan/result/log models.
- Define signed prerequisite/release manifest format.

### Setup M2 — WPF wizard

- Self-contained elevated WPF executable.
- Welcome, preflight, mode selection, organization, authentication, SQL, IIS/bindings, paths, review pages.
- Export/import installation plan JSON.

### Setup M3 — server provisioning

- IIS role installation/configuration.
- Hosting Bundle download/install/repair.
- SQL Server Express download/verification/silent install.
- Existing SQL Server connection validation.
- Filesystem/service identity permissions.

### Setup M4 — LedgerForge deployment

- Publish/deploy web payload.
- Generate configuration.
- Create site/app pool.
- Apply migrations.
- Initialize generic reference data/bootstrap authorization.
- Health validation.

### Setup M5 — upgrade/offline/unattended

- Existing-install detection.
- Upgrade path.
- Backup/resume/reboot support.
- Offline bundle.
- `/quiet /config` mode.
- Code signing/release packaging.

## Relationship to the current project roadmap

The installer does **not** replace the current validation/migration priority. Before Setup can safely automate LedgerForge deployment, the application needs:

1. a reproducibly clean Release build/test baseline,
2. a reviewed initial EF migration,
3. a disposable SQL Server migration test,
4. explicit seed/bootstrap commands that setup can invoke,
5. a stable health-check endpoint and deployment payload.

Once those exist, Setup M1/M2 can begin in parallel with the remaining product hardening.

## Microsoft platform facts informing this design

As of the current LedgerForge planning checkpoint:

- Microsoft supports unattended SQL Server installation/configuration from the command line using quiet modes and license-acceptance setup switches.
- SQL Server 2025 Express raises the maximum relational database size to 50 GB.
- Windows Server can install IIS roles/features programmatically with elevated `Install-WindowsFeature`/ServerManager tooling.
- The ASP.NET Core Hosting Bundle installs the ASP.NET Core Module required for IIS hosting, and Microsoft advises reinstalling/repairing it when IIS is installed after the Hosting Bundle.

These external prerequisites should always be pinned/tested per LedgerForge release instead of resolved as uncontrolled "latest" dependencies at install time.
