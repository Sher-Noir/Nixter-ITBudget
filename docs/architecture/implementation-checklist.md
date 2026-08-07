# Phased implementation checklist

- [x] 1. Solution scaffolding and coding standards
- [ ] 2. Full SQL Server schema, constraints, lookup seeds, migrations
- [ ] 3. Windows Authentication, AD group resolution, authorization policies, friendly 403
- [ ] 4. Fiscal years and budget versions
- [ ] 5. Spreadsheet-like budget planning grid
- [ ] 6. FY2027 workbook migration with preview, lineage, transaction, reconciliation
- [ ] 7. Vendors and contracts
- [ ] 8. Purchase orders, PO lines, change orders, commitments
- [ ] 9. Invoices, receipts, document storage/security scanning abstraction
- [ ] 10. Actual transaction ledger and configurable imports
- [ ] 11. Renewals/calendar/reminders
- [ ] 12. Amendments and forecasting/scenarios
- [ ] 13. Approvals, comments, tasks, notifications
- [ ] 14. Dashboard and drill-through KPIs
- [ ] 15. Report center and configurable Finance exports
- [ ] 16. Fiscal-year rollover wizard
- [ ] 17. Administration screens and connection tests
- [ ] 18. Central immutable audit and security hardening
- [ ] 19. Unit/integration/Playwright coverage and accessibility regression checks
- [ ] 20. IIS deployment package, PowerShell scripts, backup/restore/runbooks

## Current progress inside open milestones

### Milestone 2 — schema/persistence

Completed foundation:
- FiscalYear, FiscalPeriod, BudgetVersion, BudgetItem, and BudgetItemAllocation persistence models.
- Foreign-key constraints, financial/date/allocation check constraints, rowversion concurrency, and restrictive deletes.
- Managed lookup/master-data tables using stable codes, editable labels, active flags, sort order, and alias mapping.
- Workbook-derived and workflow lookup seed catalogs.
- Idempotent reference-data initializer that inserts missing stable codes without overwriting administrator-renamed values.
- ImportBatch, ImportRow, and ImportException persistence with source lineage and indexes.
- AdGroupMapping and UserRoleException security persistence.

Remaining before milestone completion:
- Remaining version-one domain tables and relationship constraints.
- Initial EF migration generated and applied to a disposable SQL Server database.
- Database-level migration verification after CI/build tooling is available.

### Milestone 3 — Windows/AD authorization

Completed foundation:
- Integrated Windows Authentication through ASP.NET Core Negotiate.
- Default/fallback authorization requires both authentication and a logical application role.
- Named policies for editing, budget administration, approvals, imports, audit, and system administration.
- Database-backed AD group mappings plus deployment-config bootstrap mappings.
- Per-user role grant/deny model; active deny exceptions override grants/group membership.
- AD membership/configuration failures fail closed and are logged.
- Friendly 401/403 application page.
- System Administrator AD group mapping screen with activate/deactivate support.
- AD bootstrap/deployment documentation.

Remaining before milestone completion:
- User-role exception administration UI.
- Automated authorization tests with test Windows principals.
- IIS-hosted Windows Authentication verification against CRCH AD.

### Milestone 6 — FY2027 migration

Completed foundation:
- Both supplied FY2027 workbooks inspected and documented.
- Exact eight-sheet layout verified.
- Exact `Raw Budget Info` and `Lists` headers encoded as import schema.
- ClosedXML reader validates structure and reads the current 68-row source.
- Planned totals are recalculated from quantity × unit cost using decimal values.
- Source workbook SHA-256 and row/sheet lineage are represented.
- Reconciliation checks the 68 / $830,683.48 / 44 Must Have targets.
- Source item numbering gaps are preserved and warned, not renumbered.
- `Raw Budget Detail` conflicts are documented and prohibited from silent description-only merging.
- Persisted preview writes ImportBatch, ImportRow, and ImportException records in a transaction.
- Preview resolves Finance Type, Department, Location, Need Level, Internal Category, and Frequency against active managed lookups/aliases.
- Contradictory workflow flags and unknown lookup values reject source rows; total differences and missing vendors are warnings.
- Import batch workflow requires explicit acceptance before commit and requires a reason when reconciliation targets are missed.
- Protected FY2027 upload/preview UI with configurable size limit, `.xlsx` restriction, ZIP signature validation, CSRF protection, and `ManageImports` authorization.
- Unit tests cover workbook parsing, allocation reconciliation, and import batch workflow guards.

Remaining before milestone completion:
- Deterministic secondary-source enrichment from `Raw Budget Detail`/renewal-related sheets and reviewed exception mappings.
- Transactional commit into FiscalYear/BudgetVersion/BudgetItem and related master records.
- Secured immutable source-workbook attachment.
- Preview detail/exception-resolution UI and administrator acceptance action.
- Integration regression using the production source files outside source control.

### Milestone 17 — administration

Completed foundation:
- System Administrator security mapping page uses friendly forms rather than raw JSON.
- Deployment-config bootstrap group mappings are documented and separated from normal database administration.
- Managed lookup initializer exists as an explicit service and is not auto-run during production startup.

Remaining before milestone completion:
- Full lookup management screens, role-exception administration, fiscal settings, import/export profiles, storage/SMTP settings, diagnostics, connection tests, retention, branding, and feature flags.

## Validation blocker

The agent runtime does not contain the .NET SDK. GitHub Actions is triggered for branch/PR commits, but the hosted `build-test` job currently fails before GitHub reports any executable steps or downloadable job logs through the connected API. There is therefore no code-level CI failure output available yet. This must be resolved before a milestone can satisfy its build/test exit criteria.

## Milestone exit criteria

Each milestone must build, run automated tests, apply migrations to a disposable SQL Server database, update documentation, record completed/remaining work, and leave no mock-only production services or untracked TODOs.
