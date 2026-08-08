# LedgerForge assumptions that do not block implementation

1. .NET 10 LTS is the supported production baseline; patch versions should be kept current.
2. SQL Server supports EF Core 10, filtered indexes, `rowversion`, and transactional DDL used by migrations.
3. The reference deployment uses IIS with Windows Authentication enabled and Anonymous Authentication disabled. Other authentication providers may be introduced later without changing the domain model.
4. Directory group names are deployment configuration and are never hardcoded into authorization code.
5. Fiscal-year defaults and fiscal-period boundaries are administrator-configurable.
6. Persisted timestamps are UTC; display time zone is configurable per deployment and defaults to UTC.
7. Documents are stored outside the IIS public application directory and are served only through authorized application endpoints.
8. SMTP is optional; in-app notifications remain functional without email delivery.
9. Finance/export consumers may receive controlled exports without requiring interactive application access.
10. Spreadsheet imports are adapter-specific. The included legacy-budget adapter demonstrates one validated structure, but adopters are expected to configure or implement mappings appropriate to their source data.
11. Real source workbooks are migration evidence and should not be committed to the public source repository. Import batches retain hashes and, when attachment storage is implemented, immutable secured source copies.
12. Organization branding, names, logos, account structures, departments, locations, and other adopter-specific values are configuration or master data rather than upstream source constants.
