# FY2027 workbook migration mapping

Verified source files:
- `FY2027-CRCH-ITDepartment-Budget-New (1).xlsx`
- `FY2027-CRCH-ITDepartment-Budget-Tracker.xlsx`

Verified reconciliation targets:
- 68 current budget items
- $830,683.48 recalculated planned total
- 44 items with Need Level = `4 = Must Have`

The importer must recalculate planned total as `Quantity × Unit Cost` using decimal arithmetic and must not trust stale cached formula values.

See `fy2027-source-validation.md` for the exact inspected file hashes, workbook differences, and data-quality findings.

## Sheet mapping

| Workbook sheet | Destination concepts | Migration treatment |
|---|---|---|
| Budget Summary | Reconciliation/report-only values | Comparison only. Known cached `#VALUE!` output exists; never import calculated summary cells as authoritative financial facts. |
| Finance Copy | Finance export profile and mapping reference | Capture useful column ordering/labels/mappings for default Finance export configuration; source rows are not a separate budget ledger. |
| Individual Category Lookup | Lookup aliases and category mappings | Reference-only. Known cached `#VALUE!` output exists; normalize verified labels to managed lookup records and preserve source aliases. |
| Renewal Calendar | Renewal presentation/reference | Do not import cached days/status values. The two source files contain different cached renewal results with identical formulas. Build renewal records from authoritative source dates and calculate status at runtime. |
| Raw Budget Detail | Budget section/account-category enrichment and historical planning reference | Secondary source only. It has 66 rows totaling $876,486.91 and conflicts with many current 68-row values. Enrich only through deterministic/reviewed matches; ambiguous or conflicting rows become import exceptions. |
| Raw Budget Info | BudgetItem primary migration source | Primary 68-row line-level source and reconciliation source. Preserve source item number and row lineage. |
| Lists | Managed lookup candidates and aliases | Seed/resolve managed lookup values from the verified columns. Historical references are never deleted. |
| Backend | Migration/reference-only metadata | Inspect for supporting mappings/calculations only; do not reproduce hidden formula dependencies. |

## Verified `Raw Budget Info` columns

| Column | Destination / treatment |
|---|---|
| Item | `BudgetItem.ItemNumber`; preserve source values exactly, including the gap at item 26. |
| Item Description | `BudgetItem.Description`. |
| Reason / Purpose | Source values are currently `New` or `Replacement`; map primarily to Purchase Type while retaining original source text for lineage. Do not automatically treat this field as free-form business justification. |
| Estimated Month | Estimated fiscal period / purchase date when populated. Current FY2027 rows are blank. |
| Type | Finance/type lookup candidate (`Comp. Hardware`, `Comp. Software`, etc.); resolve through an explicit mapping. |
| Department | Managed Department lookup/alias. |
| Location | Managed Location lookup/alias. |
| # of Units | `BudgetItem.Quantity`. |
| Estimated Cost / Unit | `BudgetItem.UnitCost`. |
| Estimated Cost | Source comparison value only. Authoritative planned total is recalculated from quantity and unit cost. |
| Need Level | Managed Need Level lookup. |
| Notes | `BudgetItem.Notes`. |
| Internal Category | Managed Internal Category lookup. |
| Frequency | Managed Frequency lookup. |
| Renewal Date | `BudgetItem.RenewalDate` and source for a Renewal record when applicable. |
| Vendor | Vendor candidate only when explicitly populated. Only one current row has a Vendor value; do not infer authoritative vendors from item descriptions without review. |
| Status | Source workflow state; map explicitly during preview. |
| Approval | Source workflow flag; preserve as source state and map through the application's approval migration policy. |
| Denial | Source workflow flag; contradictory combinations become import exceptions. |
| Proposed | Source planning flag; map to budget section/status only through explicit rules. |

## Verified `Lists` columns

The importer validates these exact headers before reading lookup candidates:

1. Budget Section
2. Account Code
3. Account Category
4. Frequency
5. Priority
6. Status
7. Reason / Purpose
8. Month
9. Type
10. Department
11. Location
12. Need Level
13. Internal Category

Administrators can correct, activate/deactivate, reorder, rename, or alias database lookup values after migration without deleting historical references.

## `Raw Budget Detail` enrichment rules

The 66-row `Raw Budget Detail` source cannot be positionally or descriptively joined to the 68-row current budget.

Potential enrichment fields include:

- Budget Section
- Account Code
- Account Category
- Frequency
- Renewal Date
- Notes
- Vendor
- Proposed/source workflow flags

Before enrichment is accepted, the import preview must produce a deterministic match using a reviewed composite identity such as normalized description plus quantity/unit cost/total and, where available, category/context. Description-only matches are prohibited because duplicate descriptions and changed prices exist.

If a candidate differs materially from the `Raw Budget Info` row, preserve both source values and create an import exception rather than selecting one silently.

## Renewal migration rules

- Authoritative renewal dates come from verified line-level source fields, currently 9 populated `Raw Budget Info` rows.
- `Renewal Calendar` status and days-until-renewal are presentation outputs, not imported state.
- Renewal status is calculated from the application clock, renewal date, and configured thresholds.
- The original calendar row/value can be retained as source evidence when useful, but it must not override the calculated application status.

## Workflow-state rules

The two workbooks are not identical workflow snapshots. In the Tracker workbook, source item 1 is `Approved` with Approval = true; in the New workbook it is `Ready` with Approval = false.

Therefore:

- source status/approval flags are shown in migration preview;
- the selected migration source/profile determines which source state is being considered;
- approved application history is created only by an explicit migration policy and audit event;
- no cached workbook flag can silently create or modify an approved baseline.

## Source lineage fields to retain

Every imported or rejected source row must retain enough data to reconstruct its origin:

- Import batch ID
- Source workbook SHA-256
- Original workbook filename
- Worksheet name
- Source row number
- Source item number/key where available
- Raw normalized source values or an immutable source-row snapshot
- Import outcome and exception reason

The immutable source workbook is stored as a secured migration attachment outside source control.

## Acceptance rules

1. Validate all eight required worksheets before preview.
2. Validate the exact `Raw Budget Info` and `Lists` headers before parsing.
3. Normalize whitespace/blanks without changing semantic values.
4. Produce explicit exceptions for unknown/inactive lookups, missing required fields, duplicates, ambiguous secondary-source matches, and contradictory workflow flags.
5. Recalculate each planned total on the server using decimal arithmetic.
6. Preserve source item numbers; do not renumber the gap at item 26.
7. Preview every accepted row, warning, and exception before commit.
8. Reconcile to 68 items, $830,683.48 recalculated planned total, and 44 Must Have items.
9. Commit accepted migration records in a database transaction.
10. Require explicit administrator acceptance with a reason whenever reconciliation differs from the targets.
