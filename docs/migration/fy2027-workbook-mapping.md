# FY2027 workbook migration mapping

Source files expected:
- `FY2027-CRCH-ITDepartment-Budget-New (1).xlsx`
- `FY2027-CRCH-ITDepartment-Budget-Tracker.xlsx`

Reconciliation targets from the requirements:
- 68 current budget items
- $830,683.48 planned total
- 44 items with Need Level = Must Have

The importer must recalculate planned total as `Quantity × Unit Cost` and must not trust stale cached formula values.

## Sheet mapping

| Workbook sheet | Destination concepts | Migration treatment |
|---|---|---|
| Budget Summary | Reconciliation/report-only values | Read for comparison; do not import formula results as authoritative financial facts. |
| Finance Copy | Finance export profile and mapping reference | Capture column ordering/labels/mappings; source rows are not a separate budget ledger. |
| Individual Category Lookup | Lookup aliases and category mappings | Normalize source labels to managed lookup records; preserve aliases. |
| Renewal Calendar | Renewal, Contract, BudgetItem renewal fields | Import renewal/notice dates, vendor/service descriptions, source row lineage, and decision/status flags where deterministic. |
| Raw Budget Detail | BudgetItem and related lookup references | Primary line-level migration source when verified against workbook headers. |
| Raw Budget Info | BudgetItem supplemental fields / historical context | Merge only by a verified stable key; ambiguous rows become import exceptions. |
| Lists | Managed lookup tables | Seed active lookup values, descriptions, order, and source aliases. Historical references are never deleted. |
| Backend | Migration/reference-only metadata | Inspect for mappings/calculation support; do not reproduce fragile formulas or hidden spreadsheet dependencies. |

## Relevant source column concepts

| Source concept | Destination |
|---|---|
| Item number | BudgetItem.ItemNumber |
| Item description | BudgetItem.Description |
| Reason / purpose | BudgetItem.ReasonPurpose |
| Estimated month | BudgetItem estimated fiscal period / purchase date |
| Finance type/category | FinanceCategory / FinanceAccount mapping |
| Department | Department |
| Location | Location |
| Number of units | BudgetItem.Quantity |
| Estimated cost per unit | BudgetItem.UnitCost |
| Estimated total cost | Reconciliation-only source value; authoritative value is recalculated |
| Need level | NeedLevel |
| Notes | BudgetItem.Notes |
| Internal category | InternalCategory |
| Frequency | Frequency |
| Renewal date | Renewal.RenewalDate and/or BudgetItem.RenewalDate |
| Vendor | Vendor with duplicate-resolution workflow |
| Status | BudgetItem workflow status via explicit mapping |
| Approval / Denial / Proposed | ApprovalStatus / BudgetItem status; conflicting flags become exceptions |
| Budget section | BudgetSection |
| Account code | FinanceAccount.Code |
| Account category | FinanceCategory |
| Historical totals | Migration/report comparison only unless linked to a verified historical fiscal year/version |
| Finance-formatted export | ExportProfile defaults/reference |
| Category lookup | LookupValueAlias / managed mappings |
| Renewal calendar | Renewal records and date/status inputs |

## Source lineage fields to retain
Every imported business row will retain import batch ID, source workbook hash, workbook filename, worksheet name, source row number, and normalized source key. The immutable source workbook is stored as a migration attachment.

## Acceptance rules
1. Validate required sheets and headers before preview.
2. Normalize whitespace/blanks without changing semantic values.
3. Produce explicit exceptions for unknown/inactive lookups, missing dates/accounts/vendors, duplicate source keys, and contradictory flags.
4. Recalculate each planned total on the server using decimal arithmetic.
5. Preview before commit; commit in one transaction.
6. Reconcile item count, planned total, and Must Have count.
7. Require administrator acceptance and reason if any reconciliation target differs.

Exact header names, row locations, formula behavior, and workbook-specific mappings must be verified from the actual `.xlsx` files before the importer is considered production-ready.
