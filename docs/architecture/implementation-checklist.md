# LedgerForge phased implementation checklist

- [x] 1. LedgerForge solution scaffolding, MIT license, generic branding, and coding standards
- [ ] 2. Full SQL Server schema, constraints, generic lookup seeds, and first controlled migration
- [ ] 3. Windows Authentication, directory-group resolution, authorization policies, friendly 401/403
- [ ] 4. Fiscal years, fiscal periods, and budget versions
- [ ] 5. Spreadsheet-like budget planning grid
- [ ] 6. Configurable spreadsheet migration with preview, lineage, exception review, transaction, and reconciliation
- [ ] 7. Vendors and contracts
- [ ] 8. Purchase orders, PO lines, change orders, and commitments
- [ ] 9. Invoices, receipts, and secure document storage/scanning abstraction
- [ ] 10. Actual transaction ledger and configurable transaction imports
- [ ] 11. Renewals, calendar, and reminders
- [ ] 12. Amendments, forecasting, and scenarios
- [ ] 13. Approvals, comments, tasks, and notifications
- [ ] 14. Dashboard and drill-through KPIs
- [ ] 15. Report center and configurable finance exports
- [ ] 16. Fiscal-year rollover wizard
- [ ] 17. Administration screens, organization settings, and connection tests
- [ ] 18. Central immutable audit and security hardening
- [ ] 19. Unit/integration/Playwright coverage and accessibility regression checks
- [ ] 20. IIS deployment package, scripts, backup/restore, and runbooks

## Current progress inside open milestones

### Milestone 2 — schema/persistence

Completed foundation:
- FiscalYear, FiscalPeriod, BudgetVersion, BudgetItem, and BudgetItemAllocation persistence models.
- Foreign-key constraints, financial/date/allocation check constraints, rowversion concurrency, and restrictive deletes.
- Managed lookup/master-data tables using stable codes, editable labels, active flags, sort order, and alias mapping.
- Generic default and workflow lookup seed catalogs; organization-specific departments, locations, account mappings, and finance structures are intentionally not hardcoded.
- Idempotent reference-data initializer that inserts missing stable codes without overwriting administrator-renamed values.
- ImportBatch, ImportRow, and ImportException persistence with source lineage and indexes.
- AdGroupMapping and UserRoleException security persistence.

Remaining before milestone completion:
- Remaining version-one domain tables and relationship constraints.
- Initial EF migration generated and applied to a disposable SQL Server database.
- Database-level migration verification after CI/build tooling is available.

### Milestone 3 — Windows/directory authorization

Completed foundation:
- Integrated Windows Authentication through ASP.NET Core Negotiate.
- Default/fallback authorization requires both authentication and a logical application role.
- Named policies for editing, budget administration, approvals, imports, audit, and system administration.
- Database-backed directory group mappings plus deployment-config bootstrap mappings.
- Per-user role grant/deny model; active deny exceptions override grants/group membership.
- Directory/configuration resolution failures fail closed and are logged.
- Friendly 401/403 application page.
- System Administrator security page for group mappings and per-user grant/deny exceptions.
- Bootstrap/deployment documentation.

Remaining before milestone completion:
- Automated authorization tests with representative principals.
- IIS-hosted Windows Authentication verification in a real Active Directory environment.

### Milestone 6 — configurable spreadsheet migration

Completed foundation:
- Adapter-based ClosedXML reader with explicit sheet/header schema validation.
- Source SHA-256 and row/sheet lineage.
- Server-side planned-total recalculation using decimal values.
- Optional configurable reconciliation expectations rather than organization-specific constants.
- Source item numbering gaps are preserved and warned, not renumbered.
- Persisted preview writes ImportBatch, ImportRow, and ImportException records in a transaction.
- Preview resolves managed lookup values and aliases.
- Contradictory workflow flags and unknown lookup values reject source rows; total differences and missing vendors are warnings.
- Import batch workflow requires explicit acceptance before commit and requires a reason when configured reconciliation expectations are missed.
- Protected legacy-workbook upload/preview UI with configurable size limit, `.xlsx` restriction, ZIP signature validation, CSRF protection, and `ManageImports` authorization.
- Unit tests cover spreadsheet parsing, allocation reconciliation, and import batch workflow guards without embedding real organization data.

Remaining before milestone completion:
- Import profile/schema administration instead of a single built-in legacy adapter.
- Preview detail and exception-resolution UI.
- Transactional commit into FiscalYear/BudgetVersion/BudgetItem and related master records.
- Secured immutable source-workbook attachment.
- Administrator acceptance action and integration regression using synthetic/public fixtures.

### Milestone 14 — dashboard

Completed foundation:
- Responsive LedgerForge shell with financial-dashboard information architecture.
- Light theme inspired by clean enterprise finance dashboards.
- Dark theme using deep navy layered surfaces and subdued borders.
- Light/dark/system theme preference with per-browser persistence.
- Dashboard KPI/panel layout with neutral empty states rather than fabricated production values.

Remaining before milestone completion:
- Bind KPI cards and drill-through panels to authoritative query services.
- Add accessible charting with local production assets.
- Role-aware dashboard personalization and saved filters.

### Milestone 17 — administration

Completed foundation:
- System Administrator security page uses friendly forms rather than raw JSON for group mappings and user-role exceptions.
- Deployment-config bootstrap group mappings are separated from normal database administration.
- Organization & Appearance page manages organization name, product/application labels, logo/icon paths, footer/support text, time zone, fiscal-year label, and default theme.
- Organization settings overlay deployment defaults from a git-ignored host-local file under `App_Data`.
- Managed lookup initializer exists as an explicit service and is not auto-run during production startup.

Remaining before milestone completion:
- Full lookup management screens, fiscal settings, import/export profiles, storage/SMTP settings, diagnostics, connection tests, retention, and feature flags.

## Open-source hygiene status

Completed:
- Runtime projects, tests, solution, namespaces, and connection-string identity use `LedgerForge.*`.
- Legacy organization-named source/test trees were removed.
- Real migration totals, adopter account mappings, and organization-specific seed data are not part of the LedgerForge runtime path.
- Organization identity is configuration, not source code.

Remaining:
- Keep documentation/examples synthetic and generic.
- Add contributor/security/public-release documentation and public test fixtures before first external release.

## Validation blocker

The current agent runtime does not contain the .NET SDK. GitHub Actions is triggered for branch/PR commits, but the hosted `build-test` job has been failing before GitHub reports executable steps or downloadable job logs through the connected API. Until runner execution is available, compile/test success cannot be claimed.

## Milestone exit criteria

Each milestone must build, run automated tests, apply relevant migrations to a disposable SQL Server database, update documentation, record completed/remaining work, and leave no mock-only production services or untracked critical TODOs.
