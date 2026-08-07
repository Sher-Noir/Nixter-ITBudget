# LedgerForge v1 completion contract

LedgerForge v1 is considered feature-complete when the following capabilities are present on the release branch and the validation/release gates are green.

## Financial planning

- Fiscal years and periods with explicit close state.
- Versioned budget planning with server-authoritative totals and allocations.
- Budget-item submit/approve/deny/defer workflow.
- Audited budget amendments that preserve approved history.
- Forecast scenarios with immutable per-line baselines, editable draft forecasts, publication/archive workflow, latest-published selection, and export.
- Fiscal close readiness checks and controlled rollover into a new planning version.

## Procurement and spend

- Vendors.
- Purchase orders with coded lines and workflow states.
- Issued-PO change orders with explicit supersession lineage; an issued revision closes the superseded PO atomically.
- Quantitative receiving against issued PO lines with over-receipt prevention and immutable receipt history.
- Invoices, allocations, approval/posting workflow, and actual-ledger posting.
- Outstanding commitments calculated as issued PO value less posted linked invoices, floored at zero.
- Manual and transactional bulk actuals posting plus append-only reversals.
- Neutral configurable finance-system posting export.

## Contracts and renewals

- Contracts and explicit contract-renewal records.
- Contract-first renewal projection with budget-item fallback only when no linked contract is authoritative.
- Renewal dashboard/calendar/export and Work Center surfacing.

## Workflow and operations

- Unified approval queue for budget items, POs, invoices, and amendments.
- Work Center for approvals, upcoming renewals, import review, and fiscal-close preparation.
- Adapter-based budget import with preview, lineage, persisted exceptions, assignment/resolution, row disposition, reconciliation, acceptance/rejection, target selection, and transactional commit.
- Actuals CSV import with configurable headers and all-or-nothing validation/commit.
- Server-generated budget, actual, finance, commitment, renewal, and forecast CSV exports.

## Administration and security

- Windows Authentication with logical application roles.
- Database/configured directory-group mappings and explicit audited grant/deny exceptions.
- Fail-closed authorization.
- Configurable organization/appearance settings.
- Managed lookup and finance-dimension administration.
- Deployment diagnostics for database connectivity, migrations, storage, and deployment configuration.
- Strict CSP, antiforgery validation, secure file-name handling, document signatures/hashes, and non-web-root document storage.

## Deployment

- Explicit EF migrations; never automatic web-startup migrations.
- Deployment bootstrap `initialize|verify`.
- IIS/Windows Authentication Setup package with embedded server payload and prerequisite checks.
- Real-machine installation validation reaching `/health` HTTP 200.
- SHA-256 release integrity files and release manifest.
- Optional Authenticode signing hook for maintainers with a signing certificate.
- Documented HTTPS production hardening, backup/restore, and upgrade procedures.
- Supported automated v1 topology documented in `production-deployment.md`.

## Validation gates

A release candidate is not complete until all of these pass on the same branch head:

1. Release build with warnings treated as errors.
2. Unit tests.
3. Integration/model tests.
4. UI/security/navigation smoke tests.
5. No pending EF model changes.
6. Full migration SQL destructive/cascade scan.
7. Apply all migrations to disposable SQL Server 2022.
8. Bootstrap initialize/verify against that database.
9. Applied-schema invariants, including zero cascade-delete FKs and financial decimal rules.
10. SQL-backed actual-import atomicity test.
11. Windows Setup/payload packaging.
12. Public-source hygiene scan for legacy organization identifiers, private data artifact types, and common secret formats.

## Public/open-source boundary

Tracked source contains only generic/example configuration. Real migration workbooks, production connection credentials, private documents, adopter directory groups, organization-specific account mappings, and financial data are never repository artifacts.

LedgerForge is distributed under the repository's MIT license. Adopters own their deployment configuration, identity/directory design, SQL Server operations, certificates, backups, retention, and financial control procedures.
