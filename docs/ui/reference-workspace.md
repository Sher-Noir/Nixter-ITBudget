# LedgerForge reference-style workspace

This UI pass makes LedgerForge's primary planning experience resemble a dense financial operations workspace while retaining LedgerForge's generic open-source branding, strict CSP, server-side financial calculations, and authoritative relational data.

## Primary shell

The fixed navy sidebar exposes Dashboard, Budget, Actuals, Purchase Orders, Invoices, Vendors, Contracts, Renewals, Documents, Approvals, Reports, Fiscal Years, and one Administration entry. Administration pages expose their secondary navigation contextually rather than expanding the primary sidebar.

## Dashboard

The dashboard uses real LedgerForge data for budget, approved/revised amounts, commitments, actuals, available balance, forecast, approvals, renewals, monthly spending, category distribution, open purchase orders, and recent audit activity. Empty relationships render honest empty states; the UI does not generate demonstration financial records.

## Budget workspace

The budget workspace includes fiscal-year/version controls, search and dimension filters, summary KPIs, a dense planning grid, and a selected-item detail drawer. Section, finance type, department, location, need level, internal category, and frequency come from managed lookups. Vendor display is derived from linked purchase-order lines when a procurement relationship exists.

## Budget item detail

The item workspace combines planning dimensions with item-level financials and related records. Current budget uses revised, approved, then planned fallback. Outstanding item commitment is issued linked PO value less posted linked invoice allocations, floored at zero. Actuals come from linked actual transactions. Forecast uses the latest published item forecast when available and otherwise uses the same conservative calculated fallback as the surrounding application.

The page also exposes linked purchase orders, linked invoices/actuals, renewal information, audit activity, quick actions, and the existing planning edit/approval workflow.

## Safety boundaries

- No organization-specific names, financial records, vendors, departments, or locations are embedded in source.
- Monetary calculations remain server-side.
- No inline JavaScript or CSS is required, preserving the strict Content Security Policy.
- Existing workflow authorization and antiforgery behavior remains in place.
