# Active Directory authentication and authorization

## Authentication

Production IIS must use Integrated Windows Authentication.

- Enable **Windows Authentication** for the application.
- Disable **Anonymous Authentication**.
- Run the application pool/service identity with only the SQL Server and file-share permissions it requires.
- Do not configure local application passwords.
- Keep HTTPS enabled even on the internal network.

ASP.NET Core uses the Negotiate authentication handler and reads the authenticated Windows principal. Application authorization is separate from authentication: a successfully authenticated domain user is still denied unless at least one CRCH IT Budget application role resolves.

## Role mapping sources

Logical roles are:

- SystemAdministrator
- BudgetAdministrator
- BudgetEditor
- Approver
- ReadOnly
- Auditor

Role membership can come from two controlled sources:

1. Active `AdGroupMapping` records in SQL Server, maintained by System Administrators.
2. Deployment configuration under `Security:AdGroups`, intended primarily for bootstrap/recovery.

Per-user `UserRoleException` records can explicitly grant or deny a logical role. An active deny overrides both a user grant and AD group membership for that role.

The application does not accept role values from browser requests. If role configuration or AD membership resolution fails, authorization fails closed.

## Bootstrap configuration

Checked-in configuration contains no enabled group names. Configure bootstrap mappings through IIS/environment configuration. Example placeholders based on the product requirements:

```powershell
$env:Security__AdGroups__SystemAdministrator__0 = "CRCH-ITBudget-Admins"
$env:Security__AdGroups__BudgetAdministrator__0 = "CRCH-ITBudget-BudgetAdmins"
$env:Security__AdGroups__BudgetEditor__0 = "CRCH-ITBudget-Editors"
$env:Security__AdGroups__Approver__0 = "CRCH-ITBudget-Approvers"
$env:Security__AdGroups__ReadOnly__0 = "CRCH-ITBudget-ReadOnly"
$env:Security__AdGroups__Auditor__0 = "CRCH-ITBudget-Auditors"
```

Use the actual CRCH security groups approved for the deployment; the names above are placeholders, not source-code constants.

At minimum, configure a SystemAdministrator bootstrap group before first interactive use. After database-backed mappings are configured and verified in Administration, deployment-config mappings may be reduced or retained as a documented recovery path according to CRCH policy.

## Authorization behavior

The default and fallback ASP.NET authorization policies require an authenticated Windows identity **and** at least one logical application role. Named policies further restrict sensitive operations:

- `EditPlanningBudget`: SystemAdministrator, BudgetAdministrator, BudgetEditor
- `ManageBudget`: SystemAdministrator, BudgetAdministrator
- `Approve`: SystemAdministrator, BudgetAdministrator, Approver
- `ManageImports`: SystemAdministrator, BudgetAdministrator
- `ViewAudit`: SystemAdministrator, Auditor
- `Administration`: SystemAdministrator

Users who authenticate successfully but do not meet a policy receive the friendly application access-denied page rather than a generic server error.

## Deployment verification

After database migrations and bootstrap configuration are applied:

1. Sign in with an account in the SystemAdministrator bootstrap group and verify access.
2. Sign in with an authenticated account that is in no mapped group and verify access is denied.
3. Verify a Budget Editor cannot access administration or import-management actions.
4. Verify an Approver can reach approval workflows but cannot silently edit submitted amounts.
5. Verify an active deny exception blocks the corresponding role even when the user remains in the AD group.
6. Review application logs for AD/group resolution failures; do not change the application to fail open to work around directory problems.
