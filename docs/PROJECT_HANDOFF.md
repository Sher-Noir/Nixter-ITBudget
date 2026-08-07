# LedgerForge Project Handoff

_Last verified against `agent/initial-scaffold` on 2026-08-07 at commit `666b93562d3a329686a40e15db32ecef083d7b5f`._

This file is the authoritative handoff/checkpoint for ongoing LedgerForge implementation work. It exists so a new chat/session can resume from the real repository state without relying on conversation memory.

> **Important:** The draft PR description and `docs/architecture/implementation-checklist.md` are older than the current branch and are now partially stale. Use this file first when resuming work, then verify the live branch before changing code.

## Repository state

- Repository: `Sher-Noir/Nixter-ITBudget`
- Active branch: `agent/initial-scaffold`
- Draft PR: `#1` — LedgerForge open-source budget platform foundation
- PR base: `main`
- Current verified head: `666b93562d3a329686a40e15db32ecef083d7b5f`
- PR state at this checkpoint: open, draft, mergeable
- Current PR size at this checkpoint: approximately 349 commits / 182 changed files
- Target stack: .NET 10 LTS, ASP.NET Core MVC/Razor, EF Core, SQL Server, IIS, Integrated Windows Authentication

## Core engineering rules already established

- LedgerForge must remain organization-neutral and open-source friendly.
- Do not commit real organization names, production account mappings, employee identities, private workbooks, secrets, internal paths, or other adopter-specific sensitive data.
- SQL Server / EF Core is authoritative for financial system-of-record data.
- Financial amounts use `decimal(19,4)`.
- Browser totals are never trusted as authoritative financial calculations.
- Approved/posted financial history should be corrected through explicit workflow/reversal/amendment records, not silent destructive edits.
- Financial relationships generally use restrictive deletes.
- Mutable records use `rowversion` concurrency where appropriate.
- Authorization fails closed.
- Sensitive documents are not served directly from the web root.
- Production database migrations must be explicit deployment steps, never destructive startup migrations.
- CSP is intentionally strict; avoid inline scripts/styles.

# What is implemented

## 1. Solution / architecture / open-source conversion

Implemented:

- LedgerForge modular-monolith solution structure:
  - `LedgerForge.Domain`
  - `LedgerForge.Application`
  - `LedgerForge.Infrastructure`
  - `LedgerForge.Reporting`
  - `LedgerForge.ImportExport`
  - `LedgerForge.Web`
- .NET 10 target across the solution.
- Nullable reference types and warnings-as-errors configuration.
- MIT license, contributor documentation, security policy, architecture notes, permissions documentation, import documentation, and AD authentication documentation.
- Organization-specific runtime names/data removed from the LedgerForge source path.
- Configurable organization/product branding instead of source-code organization constants.

## 2. Authentication and authorization

Implemented:

- ASP.NET Core Negotiate / Integrated Windows Authentication reference configuration.
- Logical application roles and role-aware authorization policies.
- Fail-closed authorization behavior.
- Database-backed AD/directory group mappings.
- Deployment/bootstrap group mapping support.
- Per-user role grant/deny exceptions.
- Deny exceptions override grants/group membership.
- Security administration UI.
- Friendly access-denied behavior.
- Separate policies for budget viewing/editing, approvals, imports, audit, administration, actual posting, procurement, etc.

Still requires real-environment verification; see remaining work.

## 3. Organization and appearance settings

Implemented:

- Configurable organization name, product name, application title, logo/icon paths, footer/support text, display timezone, fiscal-year label, and default theme.
- Host-local overrides under git-ignored `App_Data`.
- Light, dark, and system theme support.
- Organization/appearance changes now emit explicit audit events because this settings store intentionally bypasses EF.

## 4. Managed master data and finance administration

Implemented:

- Generic managed lookup model with:
  - stable code
  - editable name/description
  - active/inactive state
  - sort order
  - aliases
- Generic lookup seed catalogs.
- Explicit initializer that inserts missing stable codes without overwriting administrator-renamed values.
- Managed lookup administration screens.
- Finance Categories.
- Finance Accounts with category relationship.
- Stable finance-account codes remain immutable while labels/description/sort/active/category assignment can change.
- Fiscal-year administration screens.

## 5. Fiscal years, fiscal periods, and budget versions

Implemented:

- Fiscal-year domain/persistence model.
- Fiscal-period domain/persistence model.
- Budget-version domain/persistence model.
- Date/number/check constraints.
- Locked/closed-state workflow protections.
- Fiscal-year and version administration services/UI.

## 6. Budget planning

Implemented:

- Budget items with stable identifiers and item numbers.
- Quantity, unit cost, planned total, approved total, revised total.
- Server-side authoritative calculation of totals.
- Planning dimensions including department/location/etc.
- Budget item allocation foundation.
- Spreadsheet-like planning views.
- Detailed budget-item edit screen.
- Planning edits blocked when version/item workflow state does not permit them.
- Budget item submit/approve/deny/defer workflow.
- Reviewer actor/time/reason/note lineage.

## 7. Budget amendments

Implemented:

- `BudgetAmendment` domain model.
- Amendment workflow service.
- Amendment controller/UI.
- Amendment workflow tests.
- Approved amendments update revised budget values through an explicit workflow instead of mutating approved baseline history silently.

Files include:

- `src/LedgerForge.Domain/Budgeting/BudgetAmendment.cs`
- `src/LedgerForge.Infrastructure/Budgeting/BudgetAmendmentService.cs`
- `src/LedgerForge.Web/Controllers/BudgetAmendmentsController.cs`
- `src/LedgerForge.Web/Views/BudgetAmendments/Index.cshtml`
- `tests/LedgerForge.UnitTests/BudgetAmendmentWorkflowTests.cs`

## 8. Forecasting / scenarios

Implemented:

- `ForecastScenario` domain model.
- Forecast service.
- Forecast controllers and views.
- Forecast scenario tests.
- Draft scenarios begin from a complete budget baseline.
- Item forecasts can be adjusted.
- Published scenarios are immutable.
- Forecast lines preserve baseline snapshots so later budget amendments do not rewrite historical published variance.
- Dashboard uses the latest published forecast when available.
- Forecast export support has been added to reporting/export work.

Files include:

- `src/LedgerForge.Domain/Budgeting/ForecastScenario.cs`
- `src/LedgerForge.Infrastructure/Budgeting/ForecastService.cs`
- `src/LedgerForge.Web/Controllers/ForecastsController.cs`
- `src/LedgerForge.Web/Views/Forecasts/*`
- `tests/LedgerForge.UnitTests/ForecastScenarioTests.cs`

## 9. Legacy spreadsheet import pipeline

Implemented:

- Adapter-based ClosedXML workbook reader.
- Strict sheet/header/schema validation.
- `.xlsx` and ZIP signature validation.
- Configurable upload size limit.
- SHA-256 source hashing.
- Source workbook/sheet/row lineage.
- Server-side recalculation of planned totals.
- Lookup resolution and alias support.
- Optional configurable reconciliation expectations.
- Preview batch persistence in a transaction.
- `ImportBatch`, `ImportRow`, `ImportException` workflow models.
- Warning/error handling.
- Explicit exception assignment/resolution/reopen workflow.
- Audited row disposition override:
  - Accepted
  - Accepted with warning
  - Rejected
- Open row errors block acceptance.
- Warnings require explicit accepted-with-warning disposition when applicable.
- Accepted-row aggregate is recalculated after row review changes.
- HTTP actions verify that exception/row IDs belong to the supplied batch, preventing cross-batch crafted-ID actions.
- Explicit administrator preview acceptance.
- Written acceptance reason when required.
- Transactional authoritative commit into budget records.
- Item-number/source lineage preserved.
- Import tests exist for parser/workflow/review behavior.

## 10. Actual transaction ledger

Implemented:

- Append-only actual transaction model.
- Transaction kinds include manual, invoice, adjustment, reversal.
- Positive normal transactions.
- Corrections use negative reversal rows linked to exactly one original transaction.
- Original actual row is not destructively edited/deleted.
- Reversal reason/lineage.
- Fiscal-year and fiscal-period relationship.
- Budget item / finance account / department / location dimensions.
- Closed fiscal-period posting protection.
- Actual-ledger UI.
- Separate read vs posting authorization.
- Domain tests for reversal behavior.

## 11. Vendors and purchase orders

Implemented:

- Vendor master data and UI.
- Purchase-order domain and persistence.
- Purchase-order lines.
- PO numbering per fiscal year.
- PO line dimensions:
  - budget item
  - finance account
  - department
  - location
- Draft-only line maintenance.
- Submit -> approve -> issue -> close workflow.
- Explicit rejection and cancellation lineage.
- Separate approver authority.
- Issued PO lines feed commitment calculations.
- Closed/cancelled/rejected states stop contributing as appropriate.
- Purchase-order workflow tests.

Known remaining procurement enhancement: first-class change-order support is still needed.

## 12. Invoices

Implemented:

- Invoice domain/persistence model.
- Invoice allocations.
- Vendor and optional PO relationship.
- Budget/account/department/location/fiscal-period allocation dimensions.
- Draft -> pending approval -> approved -> posted workflow.
- Rejection/cancellation lineage.
- Allocation totals must reconcile to invoice total before posting.
- Posting requires compatible/open periods.
- Atomic posting creates actual-ledger rows.
- Actual rows preserve invoice lineage.
- Duplicate invoice posting is blocked.
- PO-backed posted invoice amounts reduce outstanding PO commitment while simultaneously increasing Actual, avoiding double counting.
- Invoice UI and workflow tests.

## 13. Contracts and renewal decisions

Implemented:

- First-class contract domain model.
- Vendor relationship.
- Optional budget item / finance account relationships.
- Contract lifecycle service/UI.
- Persistent renewal review/decision records.
- Contract notice/end dates drive renewal planning.
- Contract workflow tests.

## 14. Renewal calendar

Implemented:

- Renewal calendar service/UI.
- Contract notice windows are the primary renewal signal.
- Budget-item renewal dates remain a fallback when an item is not already represented by a contract.
- Duplicate renewal exposure is avoided between contract and budget sources.
- Dashboard and renewal reporting use the same contract-first semantics.
- Renewal exports include source/vendor/action/renewal metadata.

## 15. Unified approvals

Implemented:

- Central approvals queue.
- Budget item approvals.
- Purchase-order approvals/rejections.
- Invoice approvals/rejections.
- Oldest submissions first.
- Actor/timestamp/reason/note lineage.
- Approval navigation is role-aware.
- Dashboard pending-approval counts incorporate active workflow records.

## 16. Dashboard

Implemented:

- Live authoritative dashboard service rather than static mock totals.
- Planned Budget.
- Approved Budget.
- Revised Budget.
- Outstanding Committed.
- Actual.
- Available.
- Forecast.
- Pending Approvals.
- Renewals due / renewal actions.
- Recent budget items.
- Import review panel for authorized users.
- Contract-aware renewal signals.
- Posted PO-backed invoice amounts reduce outstanding commitment.

## 17. Reporting and CSV exports

Implemented:

- Report Center.
- Fiscal-year selection.
- Authoritative summary totals.
- Budget CSV.
- Actual ledger CSV.
- Commitment/open-PO CSV.
- Renewal CSV.
- Forecast export support.
- Spreadsheet formula-injection protection for exported text.
- UTF-8/Excel-friendly CSV handling.
- Reporting calculations are aligned with dashboard commitment/actual semantics.

## 18. Central audit trail

Implemented:

- `AuditEvent` domain model/table.
- `IAuditRequestContext` abstraction.
- Web `HttpAuditRequestContext` implementation.
- EF `AuditSaveChangesInterceptor`.
- Actor, entity, action, before/after values, correlation/request metadata.
- Audit events participate in the same EF save/transaction.
- Hard deletes of auditable EF entities are blocked.
- Generic auditing excludes oversized/raw import payload fields where appropriate.
- Audit Trail controller/view with filters for actor/entity/correlation/date and bounded results.
- Audit navigation under `ViewAudit` policy.
- Organization settings emit explicit audit records outside EF.
- Document actions emit explicit audit records outside EF.
- Audit event tests.

## 19. Secure document storage

Implemented:

- Physical document store under non-web-root `App_Data/Documents`.
- Generated storage keys instead of trusting user file paths.
- Versioned document metadata.
- SHA-256 version hashes.
- File/signature validation for supported Office/PDF/image formats.
- Authorized controller-based download streaming.
- Upload/new-version controls are role-aware.
- Immutable version history.
- Linked-entity metadata support.
- Document library/detail UI.
- Explicit upload/download/access audit records.
- Local append-only access logging.

## 20. Search

Implemented:

- Global search controller/view/model.
- Search route wired into the application shell.
- Search support across relevant LedgerForge entities.

## 21. Tests currently present

The branch contains unit tests for material domain/workflow behavior including:

- actual transactions/reversals
- audit events
- budget calculations
- allocation reconciliation
- budget planning
- budget approvals
- budget amendments
- finance accounts
- fiscal-year workflow
- forecast scenarios
- import batch workflow
- import exception workflow
- import row review
- legacy workbook reader
- purchase-order workflow
- invoice workflow
- contract workflow

Integration-test and UI-test projects also exist, but meaningful environment-backed coverage remains incomplete.

# Validation status at this checkpoint

## What is known

- The draft PR is currently mergeable according to GitHub.
- Static repository hygiene checks performed during implementation found no obvious merge markers, stale CRCH namespace references, indexed inline `style=` usage, or obvious destructive `.Remove(` financial persistence calls at the time of those checks.
- The current branch includes all modules listed above.

## What is NOT yet proven

Do **not** claim the current head is production-ready or build/test clean yet.

At this checkpoint:

- The GitHub Actions run for current head `666b93562d3a329686a40e15db32ecef083d7b5f` is failing.
- The connected Actions API reports the `build-test` job as failed but still exposes no executable step summaries/logs.
- A successful full Release build of the exact current head has not yet been recorded in this handoff.
- A successful full unit/integration/UI test pass of the exact current head has not yet been recorded.
- The first controlled EF Core migration has **not** yet been committed.
- The complete current schema has not yet been migration-tested against a disposable SQL Server instance.

This validation gate is the **first priority when work resumes**.

# What is left

The following list is ordered roughly by priority.

## Priority 1 — establish a clean validation baseline

1. Obtain a local checkout of `agent/initial-scaffold` at the current head.
2. Use a .NET 10 SDK.
3. Run:

   ```powershell
   dotnet restore LedgerForge.slnx
   dotnet build LedgerForge.slnx --configuration Release --no-restore
   dotnet test LedgerForge.slnx --configuration Release --no-build
   ```

4. Fix all compiler warnings/errors; warnings are treated as errors.
5. Re-run tests until clean.
6. Determine why GitHub Actions `build-test` is failing before exposing useful step logs and repair CI if necessary.
7. Update this handoff with the exact validation result and new head SHA.

## Priority 2 — create and verify the first controlled EF migration

1. Review the final current `LedgerForgeDbContext` model.
2. Generate the first EF Core migration from the complete current schema.
3. Review generated SQL for:
   - destructive operations
   - wrong cascade behaviors
   - missing lengths/precision
   - missing indexes/unique constraints
   - enum/check-constraint mismatches
   - nullable relationship mistakes
4. Apply the migration to a disposable SQL Server database.
5. Run the application/tests against that database.
6. Validate rollback/restore strategy rather than relying on automatic destructive down migrations.
7. Commit migration and deployment notes only after review.

## Priority 3 — fiscal-year close and rollover

Still needed:

- Fiscal-year close checklist.
- Close prerequisites and blocking conditions.
- Period/fiscal-year closure workflow.
- Controlled rollover wizard.
- Copy-forward rules for recurring/renewal budget items.
- New fiscal-year budget version creation.
- Explicit treatment of open POs, contracts, renewals, forecasts, and amendments at rollover.
- Rollover audit trail.
- Rollover tests.

## Priority 4 — remaining procurement/actual enhancements

Still needed or incomplete:

- Purchase-order change orders / revisions with preserved history.
- Receipt/receiving workflow if LedgerForge is expected to track received vs invoiced quantities.
- Configurable actual-transaction import profiles/adapters beyond manual and invoice posting.
- Reconciliation workflows for external finance-system actual imports.
- Stronger duplicate-invoice/business-key policies if required by deployment rules.

## Priority 5 — import administration hardening

Still needed or incomplete:

- Administrator-managed import profiles/schema definitions instead of only the built-in legacy adapter.
- Secure immutable source-workbook attachment to the import batch.
- Synthetic end-to-end integration fixture covering upload -> preview -> exception disposition -> acceptance -> commit.
- Import profile versioning and compatibility rules.

## Priority 6 — approvals / workflow productivity

Still needed or incomplete:

- General comments/discussion model across workflow objects.
- User tasks / assignments beyond import exception assignment.
- Notification framework.
- SMTP/settings integration.
- Reminder delivery for renewals/approvals if required.
- Optional escalation/SLA logic.

## Priority 7 — reporting expansion

Still needed or incomplete:

- Finance export profiles/templates rather than only generic CSVs.
- Configurable charting/drill-downs.
- Fiscal-year close report/checklist.
- Audit activity report export.
- Saved report filters.
- Role-aware/saved dashboard personalization.

## Priority 8 — administration / operations

Still needed or incomplete:

- Storage settings administration.
- SMTP settings administration.
- Diagnostics/health page.
- Database/directory/storage connection tests.
- Retention settings.
- Feature flags where justified.
- Operational data-retention jobs.

## Priority 9 — security and environment verification

Still needed:

- Automated authorization tests using representative principals/claims/groups.
- Real IIS Integrated Windows Authentication verification.
- Real Active Directory group-resolution verification.
- Least-privilege SQL/service-account verification.
- Document storage ACL verification.
- Security-header/CSP regression tests.
- Upload/download authorization regression tests.

## Priority 10 — test depth and accessibility

Still needed:

- Real integration tests against SQL Server.
- Playwright flows for core user journeys.
- Approval authorization matrix tests.
- Import end-to-end tests.
- Invoice -> actual -> commitment accounting integration tests.
- Amendment/forecast/report consistency integration tests.
- Fiscal close/rollover tests once implemented.
- Keyboard navigation checks.
- Accessibility regression scans.
- Light/dark theme UI regression checks.

## Priority 11 — deployment and runbooks

Still needed:

- Production IIS deployment package/process.
- Migration deployment script/process.
- Environment configuration guide.
- SQL backup/restore runbook.
- Document-store backup/restore runbook.
- Disaster-recovery notes.
- Upgrade/rollback runbook.
- First-admin/bootstrap checklist.
- Operational logging/retention guidance.
- Stable release/versioning process.

# Known documentation debt

The following repository documentation predates much of the current implementation and should be refreshed after the build/migration gate is green:

- Draft PR #1 description.
- `docs/architecture/implementation-checklist.md`.
- Architecture entity inventory / ERD as needed for newly added invoices/contracts/audit/amendments/forecast entities.
- Navigation/permissions docs for all newer routes.
- README current-status list.

Do not use the old checklist as proof that a module is absent; verify the live branch first.

# Exact resume procedure for a new chat/session

When a new session begins:

1. Read this file first: `docs/PROJECT_HANDOFF.md`.
2. Connect to GitHub repository `Sher-Noir/Nixter-ITBudget`.
3. Read draft PR #1 metadata and current head SHA.
4. Treat branch `agent/initial-scaffold` as authoritative.
5. Compare the current head SHA to the SHA recorded at the top of this file.
6. If they differ, inspect changes since this checkpoint before modifying anything.
7. Verify files on the live branch rather than trusting old chat summaries.
8. Do not recreate features already listed as implemented unless repository inspection shows they are missing/broken.
9. Start with the validation gate: Release build, tests, then first controlled EF migration.
10. Update this handoff file at every substantial stopping point with:
    - current head SHA
    - what was completed
    - validation result
    - exact next task
    - any known blockers

# Copy/paste continuation prompt

Use the following prompt in a fresh chat if the previous conversation becomes too long or unavailable:

```text
Continue the LedgerForge budget-management project from the repository state, not from chat memory.

Repository: Sher-Noir/Nixter-ITBudget
Branch: agent/initial-scaffold
Draft PR: #1

FIRST:
1. Read docs/PROJECT_HANDOFF.md from the live branch in full.
2. Fetch the current PR #1 head SHA and compare it with the checkpoint SHA recorded in that file.
3. Inspect the live branch before making changes. Treat GitHub as authoritative; do not assume work described in an old chat exists unless it is actually committed.
4. Preserve all existing implemented work. Do not replace working modules with old snapshots.

ENGINEERING RULES:
- LedgerForge must remain organization-neutral/open-source friendly.
- Do not commit real organization data, employee identities, production account mappings, private workbooks, secrets, or internal paths.
- Preserve financial history. Use explicit reversals/amendments/workflow records instead of destructive edits/deletes.
- Server-side calculations and SQL constraints are authoritative.
- Financial amounts use decimal(19,4).
- Authorization must fail closed.
- Maintain strict CSP; avoid inline scripts/styles.
- Sensitive documents stay outside web root and are streamed through authorized endpoints.
- Do not auto-run destructive migrations at application startup.

IMMEDIATE PRIORITY:
Establish a clean validation baseline on the CURRENT branch head before adding new feature work:
- dotnet restore LedgerForge.slnx
- dotnet build LedgerForge.slnx --configuration Release --no-restore
- dotnet test LedgerForge.slnx --configuration Release --no-build
Fix all compiler/test failures.
Then generate/review/apply the first controlled EF Core migration against a disposable SQL Server database.

AFTER THE VALIDATION/MIGRATION GATE:
Continue with the highest-priority unfinished work listed in docs/PROJECT_HANDOFF.md, beginning with fiscal-year close/rollover unless repository inspection shows a more urgent blocker.

WORKING STYLE:
- Make small coherent commits to agent/initial-scaffold.
- Verify live files before editing central files such as Program.cs and LedgerForgeDbContext.cs.
- Add regression tests for workflow/accounting/security changes.
- Keep dashboard/report/export calculations consistent with authoritative domain semantics.
- Update docs/PROJECT_HANDOFF.md before stopping so another session can resume without relying on memory.

At the start of your response, briefly tell me:
- current branch head SHA,
- whether it differs from the handoff checkpoint,
- current build/test status,
- the exact next task you are taking.
Then continue implementation without asking me to restate prior project history unless there is a genuinely unresolved product decision.
```

# Next task at this checkpoint

**Do not start another large feature first.**

The next task is:

> **Run and repair the full .NET 10 Release build/test suite on the exact current branch head, then generate and validate the first controlled EF Core migration against disposable SQL Server.**

Once that gate is green, proceed to fiscal-year close/rollover and the remaining operational/deployment hardening in the priority order above.
