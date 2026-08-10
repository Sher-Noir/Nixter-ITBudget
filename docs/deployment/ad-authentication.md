# Active Directory authentication and authorization

## Authentication

The reference IIS deployment uses Integrated Windows Authentication, but LedgerForge now presents an application sign-in landing page before it asks IIS to authenticate the user.

- Enable **Windows Authentication** for the LedgerForge IIS application.
- Enable **Anonymous Authentication** so `/account/login` and the public health/error endpoints can render without an automatic browser challenge.
- Do not grant application access anonymously. Protected LedgerForge routes still require ASP.NET Core authorization.
- `/account/windows` is the explicit Windows-authentication entry point. When the user chooses **Continue with Windows**, ASP.NET Core Negotiate issues the Windows challenge if the browser has not already authenticated.
- Keep HTTPS enabled outside an isolated localhost evaluation host.
- Do not configure local application passwords for the Active Directory deployment mode.

This arrangement avoids the old behavior where merely opening `http://localhost:8080` immediately appeared to sign the current Windows user into LedgerForge. Browsers may still complete the Windows challenge silently after the user chooses **Continue with Windows**, depending on browser/intranet policy.

## Configurable application roles

Day-to-day authorization uses a configurable **Role → Module → Access Level** model.

Access levels are ordered:

1. `None`
2. `View`
3. `Edit`
4. `Manage`
5. `Admin`

A higher level includes the levels beneath it. A LedgerForge administrator can create a role, configure a level for each application module, then assign the role to either:

- an exact Windows identity such as `DOMAIN\user`; or
- an Active Directory security group such as `DOMAIN\LedgerForge-Budget-Owners`.

When a user receives multiple roles, LedgerForge uses the highest resolved access level for each module. Module checks are enforced on the server; hiding a navigation item is not the security boundary.

Configurable role definitions and assignments are stored in protected host-local configuration under the same writable `.ledgerforge` configuration root used for mutable organization settings. That location must remain outside `wwwroot` and outside the read-only application installation payload.

## Legacy bootstrap and recovery mappings

Existing installations may already use the original fixed roles:

- SystemAdministrator
- BudgetAdministrator
- BudgetEditor
- Approver
- ReadOnly
- Auditor

Existing SQL `AdGroupMapping` records, `UserRoleException` records, and deployment `Security:AdGroups` values remain supported as a bootstrap/recovery compatibility path. They can be managed from the **Legacy bootstrap / recovery authorization** section in Administration → Security.

Checked-in configuration contains no enabled organization-specific group names. A generic bootstrap example is:

```powershell
$env:Security__AdGroups__SystemAdministrator__0 = "LedgerForge-Admins"
```

At minimum, keep a documented SystemAdministrator recovery path until the configurable role model has been tested with more than one administrator identity.

## Active Directory service identity

LedgerForge does **not** need to store an Active Directory service-account password to authenticate users or evaluate group membership from the authenticated Windows principal.

If an organization later enables richer directory operations such as user/group search, autocomplete, or directory metadata lookup, the preferred Windows deployment is:

1. Run the LedgerForge IIS application pool under a dedicated least-privilege **group Managed Service Account (gMSA)** where the environment supports it.
2. Grant that identity only the directory read permissions actually required. Normal Active Directory read access is often sufficient; do not grant Domain Admin or broad administrative membership.
3. Use the process identity for Kerberos/LDAP operations. Prefer LDAPS when an explicit LDAP channel is required by local policy.
4. Configure required SPNs/delegation only when the chosen topology actually needs them.
5. Never place a reusable AD password in `appsettings.json`, the LedgerForge database, source control, the web root, or an installer command line.

A conventional service account may be used when gMSA is unavailable, but secret provisioning/rotation should be handled by the organization's Windows/service-account tooling rather than by LedgerForge application configuration.

## Authorization behavior

Sensitive operations have module-specific server policies in addition to route-level View checks. Examples include:

- Budget edit and actual entry require Budget `Edit` or higher.
- Procurement mutations require Procurement `Manage` or higher.
- Vendor maintenance and vendor-logo changes require Vendors `Manage` or higher.
- Contract mutations require Contracts `Manage` or higher.
- Document upload/versioning requires Documents `Edit` or higher.
- Approval decisions require Approvals `Manage` or higher.
- Fiscal-year administration and imports require their module `Manage` level or higher.
- Administration requires Administration `Admin`.

Global search filters result categories by the caller's module View permissions so search does not become a metadata side channel around navigation restrictions.

## Deployment verification

After deployment or an upgrade:

1. Browse to `/account/login` in a clean browser session and confirm the LedgerForge sign-in landing page renders without an automatic IIS credential challenge.
2. Choose **Continue with Windows** and verify the expected domain identity is authenticated.
3. Verify an authenticated identity with no LedgerForge authorization receives access denied rather than application access.
4. Create a test role and verify `None`, `View`, `Edit`, `Manage`, and `Admin` boundaries on at least two modules.
5. Assign the role to one test user and one test AD group and verify both paths.
6. Verify global search does not expose result types from a module set to `None`.
7. Keep at least one tested bootstrap/recovery administrator path before removing legacy mappings.
8. Review application logs for directory/group-resolution failures; do not change LedgerForge to fail open to work around directory problems.

## Future standalone authentication

Authentication is intentionally separate from the module-access model. A future standalone/local or external identity provider can be added behind ASP.NET Core authentication while reusing the same Role → Module → Access Level authorization layer. Standalone authentication is not part of the current implementation.