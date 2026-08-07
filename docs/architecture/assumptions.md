# Assumptions that do not block scaffolding

1. .NET 10 LTS is acceptable as the supported production baseline; patch versions will be updated before deployment.
2. SQL Server edition/version will support EF Core 10 and standard filtered indexes, `rowversion`, and transactional DDL used by migrations.
3. IIS hosts the application with Windows Authentication enabled and Anonymous Authentication disabled.
4. AD group names are deployment configuration; placeholder names from the product specification are not hardcoded into authorization code.
5. Fiscal year defaults and fiscal-period boundaries will be administrator-configurable.
6. America/New_York is the default display timezone; persisted timestamps are UTC.
7. Documents are stored on a secured Windows file share outside the IIS application directory.
8. SMTP is optional and internal; in-app notifications remain functional without SMTP.
9. Finance users do not receive application logins in version one; Finance consumes controlled exports.
10. The source workbooks named in the requirements are authoritative migration inputs but were not present during this scaffold commit. Migration code will reject unverified layouts rather than guessing headers.
