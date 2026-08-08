# Legacy budget workbook adapter

LedgerForge includes a reference ClosedXML adapter for migrating a structured legacy budget workbook into a controlled import preview. It is intentionally organization-neutral and does not contain adopter-specific totals, account mappings, departments, locations, identities, or source files.

## What the adapter demonstrates

- explicit required worksheet names and headers,
- `.xlsx` container validation before parsing,
- source SHA-256 hashing,
- source sheet/row lineage,
- duplicate item-number detection,
- preservation of source numbering gaps,
- decimal `quantity × unit cost` recalculation,
- managed lookup/alias resolution,
- warnings versus blocking exceptions,
- optional configurable reconciliation expectations,
- persisted preview rows/exceptions before any authoritative budget commit.

## Reconciliation expectations

`LegacyImport` configuration can optionally define:

- expected item count,
- expected recalculated planned total,
- a priority/need-level label,
- expected count for that priority label.

All values are optional. LedgerForge does not ship with a real organization's reconciliation figures.

## Organization-specific imports

For a new source format, prefer a dedicated adapter/profile rather than modifying the core budget domain. An adapter should translate source-specific labels into canonical managed lookup values through stable codes or aliases.

Real migration source files should remain outside the public repository. Tests should use synthetic workbooks generated in memory or sanitized fixtures that contain no private organizational data.

## Commit workflow

The current implementation persists preview/audit/exception records only. Transactional creation of authoritative fiscal-year/budget records and secured immutable source attachment storage are separate roadmap items and must remain explicit administrator actions.
