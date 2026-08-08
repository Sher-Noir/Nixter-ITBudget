# LedgerForge security policy

## Reporting a vulnerability

Do not open a public issue containing exploit details, credentials, production configuration, or sensitive organizational data. Use GitHub's private security advisory / vulnerability reporting features when enabled for the repository, or contact the repository maintainers privately through the channels listed on the repository profile.

Include enough information to reproduce and assess the issue without including unrelated private data.

## Security model

LedgerForge is designed around the following principles:

- server-side authorization on every protected operation,
- fail-closed directory/role resolution,
- no browser-supplied role authority,
- explicit anti-forgery validation for state-changing MVC actions,
- server-side recalculation of financial totals,
- restrictive database relationships for financial history,
- `rowversion` concurrency for mutable records,
- source hashing and lineage for imports,
- documents served outside the public web root through authorized endpoints,
- explicit deployment-time database migrations,
- no organization credentials or secrets in source control.

## Deployment responsibility

Self-hosters are responsible for supported operating-system/runtime patches, SQL Server security, TLS, directory configuration, service-account permissions, backups, document-storage permissions, log retention, and any required regulatory controls in their environment.

LedgerForge should be deployed with least-privilege service identities and HTTPS. The reference IIS deployment enables Windows Authentication and disables Anonymous Authentication.

## Supported versions

Until LedgerForge reaches its first stable tagged release, security fixes are applied to the active development branch. A formal supported-version matrix will be published with stable releases.
