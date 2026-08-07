# FY2027 source workbook validation

Validated on 2026-08-07 against the two supplied source files:

- `FY2027-CRCH-ITDepartment-Budget-New (1).xlsx`
  - Size: 140,503 bytes
  - SHA-256: `839e54eac40f12a0eb01d70d4f0820970e4fec96e4b411f9f28a563e7d084b11`
- `FY2027-CRCH-ITDepartment-Budget-Tracker.xlsx`
  - Size: 140,696 bytes
  - SHA-256: `d1b3ce42e8dc085234cb0d91f4d1539ac94b6ba39b8ccc332ca1c9d679e29f91`

These hashes document the exact files inspected for this validation. Production migration records must calculate and store their own source-file hash at upload time.

## Workbook structure

Both files contain the same eight required worksheets:

1. `Budget Summary`
2. `Finance Copy`
3. `Individual Category Lookup`
4. `Renewal Calendar`
5. `Raw Budget Detail`
6. `Raw Budget Info`
7. `Lists`
8. `Backend`

`Raw Budget Info` contains the 20 verified source columns used by the FY2027 line-level importer:

1. Item
2. Item Description
3. Reason / Purpose
4. Estimated Month
5. Type
6. Department
7. Location
8. # of Units
9. Estimated Cost / Unit
10. Estimated Cost
11. Need Level
12. Notes
13. Internal Category
14. Frequency
15. Renewal Date
16. Vendor
17. Status
18. Approval
19. Denial
20. Proposed

## Reconciliation of the 68-row current budget

Using `Raw Budget Info` and recalculating every line as `# of Units × Estimated Cost / Unit`:

- Budget items: **68**
- Recalculated planned total: **$830,683.48**
- Source estimated-cost total: **$830,683.48**
- Row-level planned-total mismatches: **0**
- Must Have (`4 = Must Have`) items: **44**

These results exactly match the migration targets in the product requirements.

Source item numbers run from 1 through 69 with **item 26 absent**. This is a source numbering gap, not a missing migration row. The migration must preserve source item numbers and must not renumber them.

## Current-data characteristics

For the 68 `Raw Budget Info` rows in `FY2027-CRCH-ITDepartment-Budget-New (1).xlsx`:

- Reason / Purpose: 46 Replacement, 22 New
- Type: 33 Comp. Software, 31 Comp. Hardware, 2 Consulting/Project Hours, 2 Office Equipment
- Need Level: 44 Must Have, 13 High, 11 Moderate
- Department: 61 IT/IS, 4 Medical, 1 Dental, 1 Vision, 1 Administration
- Location: 59 All Locations, 5 Waltham, 4 Brighton
- Frequency: 47 Annually, 12 Not Applicable, 7 One-Time, 2 Other - See Note
- Renewal Date populated: 9 rows
- Estimated Month populated: 0 rows
- Notes populated: 17 rows
- Vendor populated: 1 row (`KnowBe4`)
- Proposed flag: 18 true, 50 false
- Status: all 68 rows are `Ready`
- Approval: all 68 rows are false
- Denial: all 68 rows are false

The sparse Vendor column means the migration must not infer vendor identity merely because an item description looks like a vendor name. Candidate vendor extraction can be offered as a reviewable suggestion later, but it must not silently create authoritative vendor relationships.

## Stale cached formula evidence

The workbooks have identical formulas in the compared ranges but differing cached values.

Known cached error values exist in both workbooks:

- `Budget Summary!R5` = `#VALUE!`
- `Individual Category Lookup!G5` = `#VALUE!`
- `Renewal Calendar!G5` = `#VALUE!`

The `Renewal Calendar` has 14 cached-value differences between the two files even though the formulas are identical. Differences include days-until-renewal and statuses changing among Expired, Due in 30 Days, Due in 60 Days, and Upcoming. This confirms that renewal status must be calculated by the application from the current date and authoritative renewal date, not imported from cached calendar output.

`Raw Budget Info` differs in two cells between the files: the Tracker marks source item 1 as `Approved` with Approval = true, while the New workbook has that item as `Ready` with Approval = false. Workflow flags therefore remain source-file state that must be previewed and explicitly mapped; they are not a substitute for the application's approval history.

## Raw Budget Detail is not the authoritative 68-row ledger

`Raw Budget Detail` contains **66 rows totaling $876,486.91**, not 68 rows totaling $830,683.48. Many rows have different descriptions or amounts from `Raw Budget Info`, including Internet, licensing, hardware, software, and service-contract lines.

Therefore:

- `Raw Budget Info` is the primary FY2027 line-level source for the 68-row migration and reconciliation targets.
- `Raw Budget Detail` is a secondary planning/reference source for budget section and account-category context.
- Rows from `Raw Budget Detail` must not be merged into `Raw Budget Info` by description alone.
- Any enrichment from `Raw Budget Detail` requires a deterministic match (for example a reviewed composite match) or becomes an import exception requiring administrator resolution.
- Conflicting quantities, unit costs, totals, or descriptions must be surfaced in the migration preview rather than silently selected.

## Lookup source

The `Lists` worksheet has the verified columns:

- Budget Section
- Account Code
- Account Category
- Frequency
- Priority
- Status
- Reason / Purpose
- Month
- Type
- Department
- Location
- Need Level
- Internal Category

The importer reads these values as source lookup candidates/aliases. Database-managed lookup records remain authoritative after migration.

## Migration acceptance rule

The FY2027 migration preview may only show automatic reconciliation success when all of the following are true:

- 68 current `Raw Budget Info` rows are accepted;
- recalculated planned total is exactly $830,683.48;
- Must Have count is 44;
- no accepted row has a source-vs-recalculated total mismatch beyond the configured half-cent comparison tolerance;
- every rejected or ambiguous enrichment is visible as an import exception;
- the source file hash and source row/sheet lineage are retained.

A mismatch does not permit silent correction. It requires an exception report and explicit administrator acceptance before commit.
