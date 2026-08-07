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

## Milestone exit criteria
Each milestone must build, run automated tests, apply migrations to a disposable SQL Server database, update documentation, record completed/remaining work, and leave no mock-only production services or untracked TODOs.
