# Organization branding and configuration maintenance

LedgerForge organization administrators can upload an organization logo and browser icon from **Administration → Organization**. Uploaded files are stored in the configured non-web-root document storage and take precedence over the optional application-local `LogoPath` and `IconPath` fallbacks.

Supported uploads are PNG and JPEG files up to 5 MB. Removing an uploaded organization asset returns the UI to the configured path fallback or built-in LedgerForge branding.

The Organization page represents one installation profile, so it uses **Save** and **Reset** rather than destructive delete semantics. List-based configuration is maintained elsewhere:

- **Managed Lookups** — edit labels/sort order and **Delete / Retire** values.
- **Finance** — edit finance accounts and **Delete / Retire** accounts.
- **Vendors** — edit vendor details, manage vendor logos, and **Delete / Retire** vendors.
- **Security** — edit/delete directory mappings and per-user role exceptions.

For lookups, finance accounts, and vendors, LedgerForge hard-deletes only unused records. If a record is referenced by history, the delete action retires/deactivates it instead so historical financial records remain valid.
