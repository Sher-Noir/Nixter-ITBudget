# Route and navigation map

| Navigation | Primary routes |
|---|---|
| Dashboard | `/`, `/dashboard` |
| Budget | `/budget`, `/budget/items/{id}`, `/budget/quick-add`, `/budget/versions`, `/budget/amendments` |
| Actuals | `/actuals`, `/actuals/unmatched`, `/actuals/{id}` |
| Purchase Orders | `/purchase-orders`, `/purchase-orders/{id}` |
| Invoices | `/invoices`, `/invoices/{id}` |
| Vendors | `/vendors`, `/vendors/{id}` |
| Contracts | `/contracts`, `/contracts/{id}` |
| Renewals | `/renewals`, `/renewals/calendar`, `/renewals/{id}` |
| Documents | `/documents`, `/documents/{id}` |
| Approvals | `/approvals`, `/approvals/{id}` |
| Tasks | `/tasks`, `/tasks/{id}` |
| Reports | `/reports`, `/reports/{slug}` |
| Imports & Exports | `/imports`, `/imports/{id}`, `/exports`, `/exports/{id}` |
| Fiscal Years | `/fiscal-years`, `/fiscal-years/{id}`, `/fiscal-years/rollover` |
| Administration | `/admin`, `/admin/security`, `/admin/lookups`, `/admin/settings`, `/admin/import-profiles`, `/admin/export-profiles`, `/admin/diagnostics` |

Every protected route requires an authenticated Windows identity and a server-side authorization policy. Fiscal-year context is carried in route/query state and persisted as a user preference only after authorization.
