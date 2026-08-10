using System.Text.Json;
using System.Text.Json.Serialization;

namespace LedgerForge.Web.Security;

public sealed class SecurityRoleDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<LedgerForgeModule, ModuleAccessLevel> ModuleAccess { get; set; } = [];
}

public sealed class SecurityRoleAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoleId { get; set; }
    public SecurityPrincipalType PrincipalType { get; set; }
    public string PrincipalName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class SecurityAccessConfiguration
{
    public List<SecurityRoleDefinition> Roles { get; set; } = [];
    public List<SecurityRoleAssignment> Assignments { get; set; } = [];
}

public sealed class SecurityAccessConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _settingsPath;
    private readonly ILogger<SecurityAccessConfigurationStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SecurityAccessConfigurationStore(
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger<SecurityAccessConfigurationStore> logger)
    {
        _logger = logger;
        var documentStorage = configuration["Documents:StoragePath"];
        var settingsRoot = string.IsNullOrWhiteSpace(documentStorage)
            ? Path.Combine(environment.ContentRootPath, "App_Data")
            : Path.Combine(Path.GetFullPath(Environment.ExpandEnvironmentVariables(documentStorage.Trim())), ".ledgerforge");

        var webRoot = environment.WebRootPath;
        if (!string.IsNullOrWhiteSpace(webRoot) && IsSameOrChildPath(settingsRoot, webRoot))
            throw new InvalidOperationException("Security access configuration cannot be stored inside the public web root.");

        _settingsPath = Path.Combine(settingsRoot, "security-access.json");
    }

    public async Task<SecurityAccessConfiguration> GetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadUnsafeAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Guid> CreateRoleAsync(string name, string? description, CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizeRequired(name, "Role name", 100);
        var normalizedDescription = NormalizeOptional(description, 1000, "Description");

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var configuration = await ReadUnsafeAsync(cancellationToken);
            if (configuration.Roles.Any(x => string.Equals(x.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("A role with that name already exists.");

            var role = new SecurityRoleDefinition
            {
                Id = Guid.NewGuid(),
                Name = normalizedName,
                Description = normalizedDescription,
                ModuleAccess = Enum.GetValues<LedgerForgeModule>().ToDictionary(x => x, _ => ModuleAccessLevel.None)
            };
            configuration.Roles.Add(role);
            await WriteUnsafeAsync(configuration, cancellationToken);
            return role.Id;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateRoleAsync(
        Guid roleId,
        string name,
        string? description,
        IReadOnlyDictionary<LedgerForgeModule, ModuleAccessLevel> moduleAccess,
        CancellationToken cancellationToken = default)
    {
        if (roleId == Guid.Empty) throw new ArgumentException("Role ID is required.", nameof(roleId));
        var normalizedName = NormalizeRequired(name, "Role name", 100);
        var normalizedDescription = NormalizeOptional(description, 1000, "Description");

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var configuration = await ReadUnsafeAsync(cancellationToken);
            var role = configuration.Roles.SingleOrDefault(x => x.Id == roleId)
                ?? throw new KeyNotFoundException("Security role was not found.");
            if (configuration.Roles.Any(x => x.Id != roleId && string.Equals(x.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("A role with that name already exists.");

            role.Name = normalizedName;
            role.Description = normalizedDescription;
            role.ModuleAccess = Enum.GetValues<LedgerForgeModule>().ToDictionary(
                module => module,
                module => moduleAccess.TryGetValue(module, out var level) && Enum.IsDefined(level) ? level : ModuleAccessLevel.None);
            await WriteUnsafeAsync(configuration, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var configuration = await ReadUnsafeAsync(cancellationToken);
            var role = configuration.Roles.SingleOrDefault(x => x.Id == roleId)
                ?? throw new KeyNotFoundException("Security role was not found.");
            configuration.Assignments.RemoveAll(x => x.RoleId == role.Id);
            configuration.Roles.Remove(role);
            await WriteUnsafeAsync(configuration, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Guid> CreateAssignmentAsync(
        Guid roleId,
        SecurityPrincipalType principalType,
        string principalName,
        string? description,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(principalType)) throw new ArgumentOutOfRangeException(nameof(principalType));
        var normalizedPrincipal = NormalizeRequired(principalName, "User or group", 256);
        var normalizedDescription = NormalizeOptional(description, 1000, "Description");

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var configuration = await ReadUnsafeAsync(cancellationToken);
            if (!configuration.Roles.Any(x => x.Id == roleId)) throw new KeyNotFoundException("Security role was not found.");
            if (configuration.Assignments.Any(x => x.RoleId == roleId && x.PrincipalType == principalType && string.Equals(x.PrincipalName, normalizedPrincipal, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("That user or group is already assigned to this role.");

            var assignment = new SecurityRoleAssignment
            {
                Id = Guid.NewGuid(),
                RoleId = roleId,
                PrincipalType = principalType,
                PrincipalName = normalizedPrincipal,
                Description = normalizedDescription
            };
            configuration.Assignments.Add(assignment);
            await WriteUnsafeAsync(configuration, cancellationToken);
            return assignment.Id;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var configuration = await ReadUnsafeAsync(cancellationToken);
            var removed = configuration.Assignments.RemoveAll(x => x.Id == assignmentId);
            if (removed == 0) throw new KeyNotFoundException("Security role assignment was not found.");
            await WriteUnsafeAsync(configuration, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<SecurityAccessConfiguration> ReadUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_settingsPath)) return new SecurityAccessConfiguration();
        try
        {
            await using var stream = File.OpenRead(_settingsPath);
            var configuration = await JsonSerializer.DeserializeAsync<SecurityAccessConfiguration>(stream, JsonOptions, cancellationToken)
                ?? new SecurityAccessConfiguration();
            Normalize(configuration);
            return configuration;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            _logger.LogWarning(exception, "Module-access configuration could not be loaded. Configurable role grants will fail closed; legacy bootstrap authorization remains available.");
            return new SecurityAccessConfiguration();
        }
    }

    private async Task WriteUnsafeAsync(SecurityAccessConfiguration configuration, CancellationToken cancellationToken)
    {
        Normalize(configuration);
        var directory = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = _settingsPath + ".tmp";
        await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, configuration, JsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        File.Move(temporaryPath, _settingsPath, overwrite: true);
    }

    private static void Normalize(SecurityAccessConfiguration configuration)
    {
        configuration.Roles ??= [];
        configuration.Assignments ??= [];
        foreach (var role in configuration.Roles)
        {
            role.Name = NormalizeRequired(role.Name, "Role name", 100);
            role.Description = NormalizeOptional(role.Description, 1000, "Description");
            role.ModuleAccess ??= [];
            foreach (var module in Enum.GetValues<LedgerForgeModule>())
                role.ModuleAccess.TryAdd(module, ModuleAccessLevel.None);
        }
        configuration.Assignments.RemoveAll(x => !configuration.Roles.Any(role => role.Id == x.RoleId));
        foreach (var assignment in configuration.Assignments)
        {
            assignment.PrincipalName = NormalizeRequired(assignment.PrincipalName, "User or group", 256);
            assignment.Description = NormalizeOptional(assignment.Description, 1000, "Description");
        }
    }

    private static string NormalizeRequired(string? value, string label, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{label} is required.");
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentException($"{label} cannot exceed {maxLength} characters.");
        return normalized;
    }

    private static string NormalizeOptional(string? value, int maxLength, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentException($"{label} cannot exceed {maxLength} characters.");
        return normalized;
    }

    private static bool IsSameOrChildPath(string candidate, string parent)
    {
        var candidateFull = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var parentFull = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return candidateFull.StartsWith(parentFull, StringComparison.OrdinalIgnoreCase);
    }
}
