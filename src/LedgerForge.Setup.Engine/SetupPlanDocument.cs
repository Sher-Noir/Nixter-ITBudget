using System.Text.Json;

namespace LedgerForge.Setup.Engine;

public sealed record SetupPlanDocument(int SchemaVersion, SetupPlan Plan)
{
    public const int CurrentSchemaVersion = 1;

    public static SetupPlanDocument Create(SetupPlan plan)
    {
        EnsureValid(plan);
        return new(CurrentSchemaVersion, plan);
    }

    public static SetupPlanDocument Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Setup plan JSON is required.", nameof(json));
        SetupPlanDocument document;
        try
        {
            document = JsonSerializer.Deserialize<SetupPlanDocument>(json, JsonOptions)
                ?? throw new InvalidDataException("Setup plan JSON did not contain a plan document.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Setup plan JSON is invalid.", exception);
        }

        if (document.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException($"Unsupported setup plan schema version {document.SchemaVersion}. Expected {CurrentSchemaVersion}.");
        EnsureValid(document.Plan);
        return document;
    }

    public string ToJson()
    {
        if (SchemaVersion != CurrentSchemaVersion)
            throw new InvalidOperationException($"Unsupported setup plan schema version {SchemaVersion}.");
        EnsureValid(Plan);
        return JsonSerializer.Serialize(this, JsonOptions);
    }

    public async Task WriteAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Setup plan path is required.", nameof(path));
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, ToJson(), cancellationToken);
    }

    public static async Task<SetupPlanDocument> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Setup plan path is required.", nameof(path));
        return Parse(await File.ReadAllTextAsync(Path.GetFullPath(path), cancellationToken));
    }

    private static void EnsureValid(SetupPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var errors = SetupPlanValidator.Validate(plan);
        if (errors.Count != 0) throw new InvalidDataException("Setup plan is invalid: " + string.Join(" ", errors));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
}
