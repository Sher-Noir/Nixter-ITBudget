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
- Core FiscalYear/BudgetVersion/BudgetItem foreign-key constraints and financial check constraints.
- ImportBatch, ImportRow, and ImportException persistence models with rowversion concurrency, source lineage, indexes, and restricted deletes.

Remaining before milestone completion:
- Managed lookup/master-data tables and lookup seed catalog.
- Remaining version-one domain tables and relationship constraints.
- Initial EF migration generated and applied to a disposable SQL Server database.

### Milestone 6 — FY2027 migration

Completed foundation:
- Both supplied FY2027 workbooks inspected and documented.
- Exact eight-sheet layout verified.
- Exact `Raw Budget Info` and `Lists` headers encoded as import schema.
- ClosedXML reader validates structure and reads the current 68-row source.
- Planned totals are recalculated from quantity × unit cost using decimal values.
- Source workbook SHA-256 and row/sheet lineage are represented.
- Reconciliation model checks the 68 / $830,683.48 / 44 Must Have targets.
- Source item numbering gaps are preserved and warned, not renumbered.
- Unit tests cover recalculation, header rejection, duplicate source item numbers, and sequence-gap warnings.
- `Raw Budget Detail` conflicts are documented and prohibited from silent description-only merging.

Remaining before milestone completion:
- Persist a parsed preview into ImportBatch/ImportRow/ImportException.
- Lookup resolution and alias mapping.
- Deterministic secondary-source enrichment and exception workflow.
- Transactional commit into FiscalYear/BudgetVersion/BudgetItem records.
- Secured immutable source-workbook attachment.
- Administrator acceptance path for reconciliation exceptions.
- Integration regression using the production source files outside source control.

## Milestone exit criteria

Each milestone must build, run automated tests, apply migrations to a disposable SQL Server database, update documentation, record completed/remaining work, and leave no mock-only production services or untracked TODOs.
