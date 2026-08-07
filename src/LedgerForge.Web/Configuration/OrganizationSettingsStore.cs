using System.Text.Json;
using Microsoft.Extensions.Options;

namespace LedgerForge.Web.Configuration;

public sealed class OrganizationSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;
    private readonly BrandingOptions _defaults;
    private readonly ILogger<OrganizationSettingsStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public OrganizationSettingsStore(
        IWebHostEnvironment environment,
        IOptions<BrandingOptions> defaults,
        ILogger<OrganizationSettingsStore> logger)
    {
        _defaults = ValidateAndNormalize(Clone(defaults.Value));
        _logger = logger;
        _settingsPath = Path.Combine(environment.ContentRootPath, "App_Data", "organization-settings.json");
    }

    public async Task<BrandingOptions> GetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_settingsPath)) return Clone(_defaults);

            try
            {
                await using var stream = File.OpenRead(_settingsPath);
                var stored = await JsonSerializer.DeserializeAsync<BrandingOptions>(stream, JsonOptions, cancellationToken);
                return stored is null ? Clone(_defaults) : Normalize(stored, _defaults);
            }
            catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
            {
                _logger.LogWarning(exception, "Organization settings could not be loaded; deployment defaults will be used.");
                return Clone(_defaults);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(BrandingOptions settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = ValidateAndNormalize(settings);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath)!;
            Directory.CreateDirectory(directory);

            var temporaryPath = _settingsPath + ".tmp";
            await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, normalized, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, _settingsPath, overwrite: true);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(_settingsPath)) File.Delete(_settingsPath);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static BrandingOptions ValidateAndNormalize(BrandingOptions value)
    {
        if (string.IsNullOrWhiteSpace(value.ProductName)) throw new ArgumentException("Product name is required.");
        if (string.IsNullOrWhiteSpace(value.OrganizationName)) throw new ArgumentException("Organization name is required.");
        if (string.IsNullOrWhiteSpace(value.ApplicationTitle)) throw new ArgumentException("Application title is required.");
        if (string.IsNullOrWhiteSpace(value.TimeZone)) throw new ArgumentException("Time zone is required.");
        if (string.IsNullOrWhiteSpace(value.DefaultFiscalYearLabel)) throw new ArgumentException("Default fiscal-year label is required.");

        var theme = value.DefaultTheme?.Trim().ToLowerInvariant();
        if (theme is not ("light" or "dark" or "system"))
            throw new ArgumentException("Default theme must be light, dark, or system.");

        return new BrandingOptions
        {
            ProductName = value.ProductName.Trim(),
            OrganizationName = value.OrganizationName.Trim(),
            ApplicationTitle = value.ApplicationTitle.Trim(),
            LogoPath = NormalizeAssetPath(value.LogoPath, "Logo path"),
            IconPath = NormalizeAssetPath(value.IconPath, "Icon path"),
            SupportText = NormalizeOptional(value.SupportText),
            FooterText = NormalizeOptional(value.FooterText),
            TimeZone = value.TimeZone.Trim(),
            DefaultFiscalYearLabel = value.DefaultFiscalYearLabel.Trim(),
            DefaultTheme = theme
        };
    }

    private static BrandingOptions Normalize(BrandingOptions stored, BrandingOptions defaults)
    {
        stored.ProductName = string.IsNullOrWhiteSpace(stored.ProductName) ? defaults.ProductName : stored.ProductName;
        stored.OrganizationName = string.IsNullOrWhiteSpace(stored.OrganizationName) ? defaults.OrganizationName : stored.OrganizationName;
        stored.ApplicationTitle = string.IsNullOrWhiteSpace(stored.ApplicationTitle) ? defaults.ApplicationTitle : stored.ApplicationTitle;
        stored.LogoPath = string.IsNullOrWhiteSpace(stored.LogoPath) ? defaults.LogoPath : stored.LogoPath;
        stored.IconPath = string.IsNullOrWhiteSpace(stored.IconPath) ? defaults.IconPath : stored.IconPath;
        stored.SupportText = string.IsNullOrWhiteSpace(stored.SupportText) ? defaults.SupportText : stored.SupportText;
        stored.FooterText = string.IsNullOrWhiteSpace(stored.FooterText) ? defaults.FooterText : stored.FooterText;
        stored.TimeZone = string.IsNullOrWhiteSpace(stored.TimeZone) ? defaults.TimeZone : stored.TimeZone;
        stored.DefaultFiscalYearLabel = string.IsNullOrWhiteSpace(stored.DefaultFiscalYearLabel) ? defaults.DefaultFiscalYearLabel : stored.DefaultFiscalYearLabel;
        stored.DefaultTheme = string.IsNullOrWhiteSpace(stored.DefaultTheme) ? defaults.DefaultTheme : stored.DefaultTheme;
        return ValidateAndNormalize(stored);
    }

    private static BrandingOptions Clone(BrandingOptions source) => new()
    {
        ProductName = source.ProductName,
        OrganizationName = source.OrganizationName,
        ApplicationTitle = source.ApplicationTitle,
        LogoPath = source.LogoPath,
        IconPath = source.IconPath,
        SupportText = source.SupportText,
        FooterText = source.FooterText,
        TimeZone = source.TimeZone,
        DefaultFiscalYearLabel = source.DefaultFiscalYearLabel,
        DefaultTheme = source.DefaultTheme
    };

    private static string NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string NormalizeAssetPath(string? value, string fieldName)
    {
        var normalized = NormalizeOptional(value);
        if (normalized.Length == 0) return normalized;
        if (!normalized.StartsWith('/', StringComparison.Ordinal) || normalized.StartsWith("//", StringComparison.Ordinal))
            throw new ArgumentException($"{fieldName} must be an application-local path beginning with '/'.");
        return normalized;
    }
}
