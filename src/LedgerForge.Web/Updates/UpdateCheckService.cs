using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace LedgerForge.Web.Updates;

public sealed class UpdateOptions
{
    public const string SectionName = "Updates";

    public bool Enabled { get; set; } = true;
    public string Repository { get; set; } = "Sher-Noir/Nixter-ITBudget";
    public int CheckIntervalHours { get; set; } = 12;
    public int InitialDelaySeconds { get; set; } = 5;
}

public sealed record LedgerForgeUpdateStatus(
    string CurrentVersion,
    string? LatestVersion,
    bool IsUpdateAvailable,
    string? ReleaseUrl,
    DateTimeOffset? CheckedAtUtc);

public sealed class UpdateStatusStore
{
    private LedgerForgeUpdateStatus _current = new(
        LedgerForgeApplicationVersion.Current,
        LatestVersion: null,
        IsUpdateAvailable: false,
        ReleaseUrl: null,
        CheckedAtUtc: null);

    public LedgerForgeUpdateStatus Current => Volatile.Read(ref _current);

    internal void Publish(LedgerForgeUpdateStatus status)
        => Volatile.Write(ref _current, status);
}

public sealed partial class GitHubReleaseUpdateService(
    IHttpClientFactory httpClientFactory,
    IOptions<UpdateOptions> options,
    UpdateStatusStore statusStore,
    ILogger<GitHubReleaseUpdateService> logger) : BackgroundService
{
    [GeneratedRegex(@"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex RepositoryPattern();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var configuration = options.Value;
        if (!configuration.Enabled)
            return;

        var initialDelay = TimeSpan.FromSeconds(Math.Clamp(configuration.InitialDelaySeconds, 0, 300));
        if (initialDelay > TimeSpan.Zero)
        {
            try
            {
                await Task.Delay(initialDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }

        var interval = TimeSpan.FromHours(Math.Clamp(configuration.CheckIntervalHours, 1, 168));
        while (!stoppingToken.IsCancellationRequested)
        {
            await CheckOnceAsync(configuration, stoppingToken);

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task CheckOnceAsync(UpdateOptions configuration, CancellationToken cancellationToken)
    {
        var repository = configuration.Repository?.Trim() ?? string.Empty;
        if (!RepositoryPattern().IsMatch(repository))
        {
            logger.LogWarning("LedgerForge update checking is disabled for this cycle because Updates:Repository is invalid.");
            return;
        }

        try
        {
            var client = httpClientFactory.CreateClient("LedgerForgeUpdates");
            using var response = await client.GetAsync(
                $"https://api.github.com/repos/{repository}/releases/latest",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "LedgerForge update check returned HTTP {StatusCode}; the application will continue normally.",
                    (int)response.StatusCode);
                return;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (!root.TryGetProperty("tag_name", out var tagElement))
                return;

            var tag = tagElement.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(tag) ||
                !LedgerForgeApplicationVersion.TryParse(tag, out var latestVersion) ||
                !LedgerForgeApplicationVersion.TryParse(LedgerForgeApplicationVersion.Current, out var currentVersion))
                return;

            string? releaseUrl = null;
            if (root.TryGetProperty("html_url", out var urlElement))
            {
                var candidate = urlElement.GetString();
                if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri) &&
                    uri.Scheme == Uri.UriSchemeHttps &&
                    string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
                    releaseUrl = uri.AbsoluteUri;
            }

            statusStore.Publish(new LedgerForgeUpdateStatus(
                LedgerForgeApplicationVersion.Current,
                tag.TrimStart('v', 'V'),
                latestVersion > currentVersion,
                releaseUrl,
                DateTimeOffset.UtcNow));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogInformation(exception, "LedgerForge update check could not complete; the application will continue normally.");
        }
    }
}

public static class LedgerForgeApplicationVersion
{
    public static string Current { get; } = ResolveCurrent();

    public static bool TryParse(string? value, out Version version)
    {
        var normalized = (value ?? string.Empty).Trim().TrimStart('v', 'V');
        var metadataIndex = normalized.IndexOf('+');
        if (metadataIndex >= 0) normalized = normalized[..metadataIndex];
        var prereleaseIndex = normalized.IndexOf('-');
        if (prereleaseIndex >= 0) normalized = normalized[..prereleaseIndex];
        return Version.TryParse(normalized, out version!);
    }

    private static string ResolveCurrent()
    {
        var assembly = typeof(LedgerForgeApplicationVersion).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var normalized = informational.Split('+', 2)[0].Trim();
            if (!string.IsNullOrWhiteSpace(normalized)) return normalized;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
