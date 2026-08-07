# ADR 0001 — Initial architecture decisions

Status: Accepted for scaffolding

## Decisions

1. **Modular monolith.** ASP.NET Core MVC/Razor organized into Web, Application, Domain, Infrastructure, Reporting, and ImportExport projects. No microservices for version one.
2. **Runtime.** Target .NET 10 LTS. Production must remain on a supported patched LTS release approved by CRCH.
3. **Hosting/authentication.** IIS in-process hosting with Integrated Windows Authentication. Anonymous access is disabled at IIS; application authorization fails closed.
4. **Persistence.** SQL Server via EF Core. Financial amounts use `decimal(19,4)`. UTC timestamps are stored; America/New_York is the default display timezone.
5. **Budget immutability.** Approved baselines are immutable. Changes occur through revised versions or formal amendments.
6. **Actuals separation.** Actual transactions are ledger records separate from budget lines; posted financial history is corrected through reversal/correction workflows, never silent deletion.
7. **Documents.** Metadata/relationships live in SQL Server; physical files live on a secured file share outside the IIS web root and are streamed through authorized endpoints.
8. **Authorization.** Logical roles map to configurable AD groups; group names are configuration data, not source constants.
9. **UI.** Server-rendered MVC/Razor with progressive enhancement and a spreadsheet-like budget grid. Production assets are locally hosted.
10. **Imports/exports.** Excel is an import/export format, never the system of record. Planned totals are recalculated on the server.
11. **Audit.** Material actions emit immutable centralized audit records including actor, before/after values, correlation ID, and outcome.
12. **Deployment.** Database migrations are explicit deployment steps; production startup never applies destructive migrations automatically.

## Initial non-decisions

Exact AD group names, SQL Server host/database, SMTP host, file-share path, finance export mappings, fiscal calendar details, and source-workbook column headers remain configuration or migration-discovery inputs.
