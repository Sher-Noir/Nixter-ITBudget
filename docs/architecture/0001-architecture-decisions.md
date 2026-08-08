# ADR 0001 — Initial LedgerForge architecture decisions

Status: Accepted

## Decisions

1. **Modular monolith.** ASP.NET Core MVC/Razor is organized into Web, Application, Domain, Infrastructure, Reporting, and ImportExport projects. No microservices are required for the initial product.
2. **Runtime.** Target .NET 10 LTS and remain on a supported patched LTS release.
3. **Hosting/authentication.** The default deployment model is IIS in-process hosting with Integrated Windows Authentication. Authentication and authorization are explicit deployment concerns, and application authorization fails closed.
4. **Persistence.** Financial/system-of-record data uses SQL Server via EF Core. Financial amounts use `decimal(19,4)`. Persisted timestamps are UTC; display time zone is deployment configuration.
5. **Budget immutability.** Approved baselines are immutable. Changes occur through revised versions or formal amendments.
6. **Actuals separation.** Actual transactions are ledger records separate from budget lines; posted financial history is corrected through reversal/correction workflows rather than silent deletion.
7. **Documents.** Metadata and relationships live in SQL Server; physical files are stored outside the public web root and streamed through authorized endpoints.
8. **Authorization.** Logical application roles map to configurable directory groups. Group names and user exceptions are data/configuration, never source constants.
9. **Organization identity.** LedgerForge ships with generic defaults. Branding and organization-facing labels are configurable without source changes. Host-local organization overrides live outside `wwwroot` and are excluded from source control.
10. **UI.** Server-rendered MVC/Razor with progressive enhancement, responsive navigation, and light/dark/system themes. Production assets are locally hosted.
11. **Imports/exports.** Spreadsheets are import/export formats, never the system of record. Import adapters validate source structure, hash source files, and recalculate financial totals server-side.
12. **Audit.** Material actions will emit centralized immutable audit records including actor, before/after values, correlation ID, and outcome.
13. **Deployment.** Database migrations are explicit deployment steps; production startup never applies destructive migrations automatically.
14. **Open-source hygiene.** Upstream source must not contain adopter identities, production secrets, organization-specific finance mappings, real migration files, or internal infrastructure paths.

## Configurable deployment inputs

Directory groups, SQL Server host/database, SMTP, document storage, finance export mappings, fiscal calendar, organization branding, display time zone, import reconciliation expectations, and organization-specific lookup values remain configuration or administrator-managed data.
