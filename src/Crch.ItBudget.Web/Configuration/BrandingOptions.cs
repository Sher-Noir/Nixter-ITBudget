namespace LedgerForge.Web.Configuration;

public sealed class BrandingOptions
{
    public const string SectionName = "Branding";

    public string ProductName { get; set; } = "LedgerForge";
    public string OrganizationName { get; set; } = "Your Organization";
    public string ApplicationTitle { get; set; } = "Budget Management";
    public string LogoPath { get; set; } = "/images/ledgerforge-logo.svg";
    public string IconPath { get; set; } = "/images/ledgerforge-icon.svg";
    public string SupportText { get; set; } = "Contact your LedgerForge administrator for assistance.";
    public string FooterText { get; set; } = "LedgerForge — free and open-source budget management.";
    public string TimeZone { get; set; } = "America/New_York";
    public string DefaultFiscalYearLabel { get; set; } = "Current FY";
    public string DefaultTheme { get; set; } = "system";
}
