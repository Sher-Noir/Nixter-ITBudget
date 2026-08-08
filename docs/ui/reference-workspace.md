# LedgerForge reference-style workspace

This UI pass makes LedgerForge's primary planning experience resemble a dense financial operations workspace while retaining LedgerForge's generic open-source branding, strict CSP, server-side financial calculations, and authoritative relational data.

## Primary shell

The fixed navy sidebar exposes Dashboard, Budget, Actuals, Purchase Orders, Invoices, Vendors, Contracts, Renewals, Documents, Approvals, Reports, Fiscal Years, and one Administration entry. Administration pages expose their secondary navigation contextually rather than expanding the primary sidebar. The top-bar search is wired to LedgerForge's existing cross-module search for budget items, vendors, purchase orders, invoices, and contracts.

## Dashboard

The dashboard uses real LedgerForge data for budget, approved/revised amounts, commitments, actuals, available balance, forecast, approvals, renewals, monthly spending, category distribution, open purchase orders, and recent audit activity. Fiscal year, location, and internal-category controls are server-side filters rather than decorative controls. Dimension-filtered commitment and published forecast values are calculated from records linked to the selected budget-item set. Empty relationships render honest empty states; the UI does not generate demonstration financial records.

## Budget workspace

The budget workspace includes fiscal-year/version controls, search and dimension filters, summary KPIs, a dense planning grid, and a selected-item detail drawer. Section, finance type, department, location, need level, internal category, and frequency come from managed lookups. Vendor display is derived from linked purchase-order lines when a procurement relationship exists. Import controls remain authorization-aware.

## Budget item detail

The item workspace combines planning dimensions with item-level financials and related records. Current budget uses revised, approved, then planned fallback. Outstanding item commitment is issued linked PO value less posted linked invoice allocations, floored at zero. Actuals come from linked actual transactions. Forecast uses the latest published item forecast when available and otherwise uses the same conservative calculated fallback as the surrounding application.

The page exposes linked purchase orders, linked invoices/actuals, budget-item-linked documents, renewal information, audit activity, contextual document upload, amendment creation, quick actions, and the existing planning edit/approval workflow. The existing budget-amendment workflow can be opened from a budget item with that item preselected and continues to use draft, submit, approve, reject, and cancel states.

## Deferred schema-backed reference features

Two reference-screen concepts require new persisted domain data rather than presentation work:

- **Planning-stage vendor**: LedgerForge currently treats vendors as procurement records. A planning item shows a vendor only after a linked purchase-order relationship exists. Matching the reference's vendor selection during budget planning should add an optional preferred/planning vendor relationship through a committed EF migration.
- **Related tasks**: LedgerForge's Work Center contains approvals, renewals, import reviews, and fiscal-close work, but there is no general user-created task entity associated with a budget item. A reference-style checklist should be backed by a real task table, ownership/due-date semantics, authorization, audit events, and migration rather than hard-coded examples.

These are intentionally not simulated by the UI.

## Safety boundaries

- No organization-specific names, financial records, vendors, departments, or locations are embedded in source.
- Monetary calculations remain server-side.
- No inline JavaScript or CSS is required, preserving the strict Content Security Policy.
- Existing workflow authorization and antiforgery behavior remains in place.
- Schema-backed follow-ups must use committed EF migrations and pass the disposable SQL validation gate before release.
