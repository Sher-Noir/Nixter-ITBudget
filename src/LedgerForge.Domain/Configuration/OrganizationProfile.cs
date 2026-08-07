using LedgerForge.Domain.Common;

namespace LedgerForge.Domain.Configuration;

public sealed class OrganizationProfile : AuditableEntity
{
    public const string DefaultProfileKey = "default";

    private OrganizationProfile() { }

    public OrganizationProfile(
        string organizationName,
        string applicationTitle,
        string productName = "LedgerForge")
    {
        Update(
            productName,
            organizationName,
            applicationTitle,
            logoPath: null,
            iconPath: null,
            supportText: null,
            footerText: null,
            timeZone: "UTC",
            defaultFiscalYearLabel: "Current FY",
            defaultTheme: "system",
            currencyCode: "USD");
    }

    public string ProfileKey { get; private set; } = DefaultProfileKey;
    public string ProductName { get; private set; } = "LedgerForge";
    public string OrganizationName { get; private set; } = "Your Organization";
    public string ApplicationTitle { get; private set; } = "Budget Management";
    public string? LogoPath { get; private set; }
    public string? IconPath { get; private set; }
    public string? SupportText { get; private set; }
    public string? FooterText { get; private set; }
    public string TimeZone { get; private set; } = "UTC";
    public string DefaultFiscalYearLabel { get; private set; } = "Current FY";
    public string DefaultTheme { get; private set; } = "system";
    public string CurrencyCode { get; private set; } = "USD";

    public void Update(
        string productName,
        string organizationName,
        string applicationTitle,
        string? logoPath,
        string? iconPath,
        string? supportText,
        string? footerText,
        string timeZone,
        string defaultFiscalYearLabel,
        string defaultTheme,
        string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(productName)) throw new ArgumentException("Product name is required.", nameof(productName));
        if (string.IsNullOrWhiteSpace(organizationName)) throw new ArgumentException("Organization name is required.", nameof(organizationName));
        if (string.IsNullOrWhiteSpace(applicationTitle)) throw new ArgumentException("Application title is required.", nameof(applicationTitle));
        if (string.IsNullOrWhiteSpace(timeZone)) throw new ArgumentException("Time zone is required.", nameof(timeZone));
        if (string.IsNullOrWhiteSpace(defaultFiscalYearLabel)) throw new ArgumentException("Fiscal-year label is required.", nameof(defaultFiscalYearLabel));
        if (string.IsNullOrWhiteSpace(currencyCode)) throw new ArgumentException("Currency code is required.", nameof(currencyCode));

        defaultTheme = defaultTheme.Trim().ToLowerInvariant();
        if (defaultTheme is not ("light" or "dark" or "system"))
        {
            throw new ArgumentException("Theme must be light, dark, or system.", nameof(defaultTheme));
        }

        currencyCode = currencyCode.Trim().ToUpperInvariant();
        if (currencyCode.Length != 3 || !currencyCode.All(char.IsLetter))
        {
            throw new ArgumentException("Currency code must be a three-letter ISO-style code.", nameof(currencyCode));
        }

        ProductName = productName.Trim();
        OrganizationName = organizationName.Trim();
        ApplicationTitle = applicationTitle.Trim();
        LogoPath = NormalizeOptional(logoPath);
        IconPath = NormalizeOptional(iconPath);
        SupportText = NormalizeOptional(supportText);
        FooterText = NormalizeOptional(footerText);
        TimeZone = timeZone.Trim();
        DefaultFiscalYearLabel = defaultFiscalYearLabel.Trim();
        DefaultTheme = defaultTheme;
        CurrencyCode = currencyCode;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
