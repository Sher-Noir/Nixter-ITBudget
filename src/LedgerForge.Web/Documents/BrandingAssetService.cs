using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Web.Documents;

public enum BrandingLogoSource
{
    BudgetItem,
    Vendor
}

public sealed record BrandingLogoReference(
    Guid DocumentId,
    Guid VersionId,
    string ContentType,
    string OriginalFileName,
    string StorageKey,
    BrandingLogoSource Source,
    Guid SourceEntityId);

public sealed class BrandingAssetService
{
    public const string DisplayLogoTitle = "LedgerForge Display Logo";
    public const long MaxLogoFileSizeBytes = 5L * 1024 * 1024;

    private readonly PhysicalDocumentStore _store;
    private readonly LedgerForgeDbContext _dbContext;
    private readonly string _storageRoot;

    public BrandingAssetService(
        IWebHostEnvironment environment,
        IConfiguration configuration,
        LedgerForgeDbContext dbContext)
    {
        var configuredDocumentLimit = configuration.GetValue<long?>("Documents:MaxFileSizeBytes");
        var configuredStoragePath = configuration.GetValue<string>("Documents:StoragePath");
        var documentLimit = configuredDocumentLimit is > 0 ? configuredDocumentLimit.Value : 25L * 1024 * 1024;
        _store = new PhysicalDocumentStore(environment.ContentRootPath, configuredStoragePath, documentLimit);
        _dbContext = dbContext;

        var contentRoot = Path.GetFullPath(environment.ContentRootPath);
        _storageRoot = string.IsNullOrWhiteSpace(configuredStoragePath)
            ? Path.GetFullPath(Path.Combine(contentRoot, "App_Data", "Documents"))
            : Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredStoragePath.Trim()));
    }

    public async Task<BrandingLogoReference?> ResolveBudgetItemLogoAsync(
        Guid budgetItemId,
        CancellationToken cancellationToken = default)
    {
        var itemLogo = await GetEntityLogoAsync(nameof(BrandingLogoSource.BudgetItem), budgetItemId, BrandingLogoSource.BudgetItem, cancellationToken);
        if (itemLogo is not null) return itemLogo;

        var vendorId = await (
            from line in _dbContext.PurchaseOrderLines.AsNoTracking()
            join order in _dbContext.PurchaseOrders.AsNoTracking() on line.PurchaseOrderId equals order.Id
            where line.BudgetItemId == budgetItemId
            orderby order.CreatedAtUtc descending
            select (Guid?)order.VendorId)
            .FirstOrDefaultAsync(cancellationToken);

        return vendorId is null
            ? null
            : await GetEntityLogoAsync(nameof(BrandingLogoSource.Vendor), vendorId.Value, BrandingLogoSource.Vendor, cancellationToken);
    }

    public Task<BrandingLogoReference?> GetBudgetItemLogoAsync(Guid budgetItemId, CancellationToken cancellationToken = default)
        => GetEntityLogoAsync(nameof(BrandingLogoSource.BudgetItem), budgetItemId, BrandingLogoSource.BudgetItem, cancellationToken);

    public Task<BrandingLogoReference?> GetVendorLogoAsync(Guid vendorId, CancellationToken cancellationToken = default)
        => GetEntityLogoAsync(nameof(BrandingLogoSource.Vendor), vendorId, BrandingLogoSource.Vendor, cancellationToken);

    public async Task<IReadOnlySet<Guid>> GetVendorIdsWithLogosAsync(CancellationToken cancellationToken = default)
    {
        var documents = await _store.ListAsync(cancellationToken);
        return documents
            .Where(IsDisplayLogo)
            .Where(x => string.Equals(x.LinkedEntityType, nameof(BrandingLogoSource.Vendor), StringComparison.OrdinalIgnoreCase))
            .Where(x => x.LinkedEntityId is not null)
            .Select(x => x.LinkedEntityId!.Value)
            .ToHashSet();
    }

    public async Task SaveBudgetItemLogoAsync(
        Guid budgetItemId,
        Stream source,
        string fileName,
        string actor,
        CancellationToken cancellationToken = default)
    {
        if (!await _dbContext.BudgetItems.AsNoTracking().AnyAsync(x => x.Id == budgetItemId, cancellationToken))
            throw new KeyNotFoundException("Budget item was not found.");
        await SaveEntityLogoAsync(nameof(BrandingLogoSource.BudgetItem), budgetItemId, source, fileName, actor, cancellationToken);
    }

    public async Task SaveVendorLogoAsync(
        Guid vendorId,
        Stream source,
        string fileName,
        string actor,
        CancellationToken cancellationToken = default)
    {
        if (!await _dbContext.Vendors.AsNoTracking().AnyAsync(x => x.Id == vendorId, cancellationToken))
            throw new KeyNotFoundException("Vendor was not found.");
        await SaveEntityLogoAsync(nameof(BrandingLogoSource.Vendor), vendorId, source, fileName, actor, cancellationToken);
    }

    public Task<bool> RemoveBudgetItemLogoAsync(Guid budgetItemId, CancellationToken cancellationToken = default)
        => RemoveEntityLogoAsync(nameof(BrandingLogoSource.BudgetItem), budgetItemId, cancellationToken);

    public Task<bool> RemoveVendorLogoAsync(Guid vendorId, CancellationToken cancellationToken = default)
        => RemoveEntityLogoAsync(nameof(BrandingLogoSource.Vendor), vendorId, cancellationToken);

    public FileStream OpenRead(BrandingLogoReference logo)
    {
        var normalized = logo.StorageKey.Replace('/', Path.DirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(_storageRoot, normalized));
        var rootPrefix = _storageRoot.EndsWith(Path.DirectorySeparatorChar)
            ? _storageRoot
            : _storageRoot + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Invalid branding-asset storage key.");
        if (!File.Exists(path)) throw new FileNotFoundException("Stored branding asset is missing.");
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
    }

    private async Task SaveEntityLogoAsync(
        string entityType,
        Guid entityId,
        Stream source,
        string fileName,
        string actor,
        CancellationToken cancellationToken)
    {
        ValidateLogoFileName(fileName);
        var existing = await FindLogoDocumentAsync(entityType, entityId, cancellationToken);
        if (existing is null)
        {
            await _store.CreateAsync(
                DisplayLogoTitle,
                "Optional display logo used by LedgerForge workspace headers.",
                entityType,
                entityId,
                source,
                fileName,
                actor,
                cancellationToken);
            return;
        }

        await _store.AddVersionAsync(existing.Id, source, fileName, actor, cancellationToken);
    }

    private async Task<bool> RemoveEntityLogoAsync(string entityType, Guid entityId, CancellationToken cancellationToken)
    {
        var document = await FindLogoDocumentAsync(entityType, entityId, cancellationToken);
        if (document is null) return false;

        cancellationToken.ThrowIfCancellationRequested();
        var directory = Path.GetFullPath(Path.Combine(_storageRoot, document.Id.ToString("N")));
        var rootPrefix = _storageRoot.EndsWith(Path.DirectorySeparatorChar)
            ? _storageRoot
            : _storageRoot + Path.DirectorySeparatorChar;
        if (!directory.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Invalid branding-asset directory.");
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        return true;
    }

    private async Task<BrandingLogoReference?> GetEntityLogoAsync(
        string entityType,
        Guid entityId,
        BrandingLogoSource source,
        CancellationToken cancellationToken)
    {
        var document = await FindLogoDocumentAsync(entityType, entityId, cancellationToken);
        if (document is null) return null;
        var version = document.Versions.OrderByDescending(x => x.VersionNumber).FirstOrDefault();
        if (version is null || !version.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return null;
        return new(document.Id, version.Id, version.ContentType, version.OriginalFileName, version.StorageKey, source, entityId);
    }

    private async Task<StoredDocument?> FindLogoDocumentAsync(
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken)
    {
        if (entityId == Guid.Empty) return null;
        var documents = await _store.ListAsync(cancellationToken);
        return documents
            .Where(IsDisplayLogo)
            .Where(x => string.Equals(x.LinkedEntityType, entityType, StringComparison.OrdinalIgnoreCase) && x.LinkedEntityId == entityId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefault();
    }

    private static bool IsDisplayLogo(StoredDocument document)
        => string.Equals(document.Title, DisplayLogoTitle, StringComparison.Ordinal);

    private static void ValidateLogoFileName(string fileName)
    {
        var extension = Path.GetExtension(Path.GetFileName(fileName ?? string.Empty)).ToLowerInvariant();
        if (extension is not ".png" and not ".jpg" and not ".jpeg")
            throw new InvalidDataException("Display logos must be PNG or JPEG images.");
    }
}
