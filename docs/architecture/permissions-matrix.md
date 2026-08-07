# LedgerForge permissions matrix

| Capability | System Admin | Budget Admin | Budget Editor | Approver | Read Only | Auditor |
|---|:---:|:---:|:---:|:---:|:---:|:---:|
| View budgets/reports/attachments | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Create/edit planning budget items | ✓ | ✓ | ✓ | — | — | — |
| Edit approved baseline directly | — | — | — | — | — | — |
| Create amendments | ✓ | ✓ | Limited | — | — | — |
| Approve/deny/defer | ✓* | ✓* | — | ✓ | — | — |
| Enter actuals | ✓ | ✓ | ✓ | — | — | — |
| Manage POs/invoices/contracts/renewals | ✓ | ✓ | ✓ | Review only | — | — |
| Run imports | ✓ | ✓ | — | — | — | Audit view |
| Run exports | ✓ | ✓ | Configurable | View only | Configurable | Audit view |
| Manage fiscal years | ✓ | ✓ | — | — | — | — |
| Manage lookups/configuration | ✓ | Limited | — | — | — | — |
| Organization branding/appearance | ✓ | — | — | — | — | View |
| Directory group mapping / role exceptions | ✓ | — | — | — | — | View |
| Audit search/detail | ✓ | Limited | Own activity | Approval history | — | ✓ |
| Authorized data correction | ✓ | Configurable | — | — | — | Observe |

`*` Separation-of-duties rules can prohibit self-approval and can require multiple approvers or monetary thresholds.
