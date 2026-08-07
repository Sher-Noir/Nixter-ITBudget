using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LedgerForge.Web.Documents;

public sealed record StoredDocumentVersion(
    Guid Id,
    int VersionNumber,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    string StorageKey,
    string UploadedBy,
    DateTimeOffset UploadedAtUtc);

public sealed record StoredDocument(
    Guid Id,
    string Title,
    string? Description,
    string? LinkedEntityType,
    Guid? LinkedEntityId,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<StoredDocumentVersion> Versions);

public sealed record DocumentDownload(
    StoredDocument Document,
    StoredDocumentVersion Version,
    FileStream Stream);

public sealed class PhysicalDocumentStore
{
    private const long DefaultMaxFileSizeBytes = 25L * 1024 * 1024;
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _root;
    private readonly long _maxFileSizeBytes;

    public PhysicalDocumentStore(string contentRootPath, long maxFileSizeBytes = DefaultMaxFileSizeBytes)
        : this(contentRootPath, null, maxFileSizeBytes)
    {
    }

    public PhysicalDocumentStore(string contentRootPath, string? storageRootPath, long maxFileSizeBytes = DefaultMaxFileSizeBytes)
    {
        if (string.IsNullOrWhiteSpace(contentRootPath)) throw new ArgumentException("Content root path is required.", nameof(contentRootPath));
        if (maxFileSizeBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxFileSizeBytes));

        var contentRoot = Path.GetFullPath(contentRootPath);
        _root = string.IsNullOrWhiteSpace(storageRootPath)
            ? Path.GetFullPath(Path.Combine(contentRoot, "App_Data", "Documents"))
            : Path.GetFullPath(Environment.ExpandEnvironmentVariables(storageRootPath.Trim()));

        var webRoot = Path.GetFullPath(Path.Combine(contentRoot, "wwwroot"));
        var webRootPrefix = webRoot.EndsWith(Path.DirectorySeparatorChar) ? webRoot : webRoot + Path.DirectorySeparatorChar;
        if (_root.Equals(webRoot, StringComparison.OrdinalIgnoreCase) || _root.StartsWith(webRootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Document storage must be outside the public web root.");

        _maxFileSizeBytes = maxFileSizeBytes;
        Directory.CreateDirectory(_root);
    }

    public async Task<IReadOnlyList<StoredDocument>> ListAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<StoredDocument>();
        foreach (var directory in Directory.EnumerateDirectories(_root))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var metadataPath = Path.Combine(directory, "document.json");
            if (!File.Exists(metadataPath)) continue;
            try
            {
                var json = await File.ReadAllTextAsync(metadataPath, cancellationToken);
                var document = JsonSerializer.Deserialize<StoredDocument>(json, JsonOptions);
                if (document is not null) results.Add(document);
            }
            catch (JsonException)
            {
                // A malformed metadata file is omitted from the library rather than exposing a partial record.
            }
        }

        return results.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Title).ToArray();
    }

    public async Task<StoredDocument?> GetAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        if (documentId == Guid.Empty) return null;
        var path = MetadataPath(documentId);
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<StoredDocument>(json, JsonOptions);
    }

    public async Task<StoredDocument> CreateAsync(
        string title,
        string? description,
        string? linkedEntityType,
        Guid? linkedEntityId,
        Stream source,
        string originalFileName,
        string actor,
        CancellationToken cancellationToken = default)
    {
        title = Required(title, 250, nameof(title));
        description = Optional(description, 2000, nameof(description));
        linkedEntityType = Optional(linkedEntityType, 128, nameof(linkedEntityType));
        if (linkedEntityId == Guid.Empty) throw new ArgumentException("Linked entity ID cannot be empty.", nameof(linkedEntityId));
        actor = Required(actor, 256, nameof(actor));

        var documentId = Guid.NewGuid();
        var documentDirectory = DocumentDirectory(documentId);
        Directory.CreateDirectory(documentDirectory);

        try
        {
            var version = await SaveVersionFileAsync(documentId, 1, source, originalFileName, actor, cancellationToken);
            var document = new StoredDocument(
                documentId,
                title,
                description,
                linkedEntityType,
                linkedEntityId,
                actor,
                DateTimeOffset.UtcNow,
                [version]);
            await WriteMetadataAtomicAsync(document, cancellationToken);
            return document;
        }
        catch
        {
            TryDeleteDirectory(documentDirectory);
            throw;
        }
    }

    public async Task<StoredDocument> AddVersionAsync(
        Guid documentId,
        Stream source,
        string originalFileName,
        string actor,
        CancellationToken cancellationToken = default)
    {
        actor = Required(actor, 256, nameof(actor));
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var document = await GetAsync(documentId, cancellationToken)
                ?? throw new KeyNotFoundException("Document was not found.");
            var nextVersion = document.Versions.Count == 0 ? 1 : document.Versions.Max(x => x.VersionNumber) + 1;
            var version = await SaveVersionFileAsync(documentId, nextVersion, source, originalFileName, actor, cancellationToken);
            var updated = document with { Versions = document.Versions.Concat([version]).OrderBy(x => x.VersionNumber).ToArray() };
            try
            {
                await WriteMetadataAtomicAsync(updated, cancellationToken);
                return updated;
            }
            catch
            {
                TryDeleteFile(StoragePath(version.StorageKey));
                throw;
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<DocumentDownload> OpenDownloadAsync(
        Guid documentId,
        Guid versionId,
        string actor,
        string correlationId,
        string? remoteAddress,
        CancellationToken cancellationToken = default)
    {
        actor = Required(actor, 256, nameof(actor));
        correlationId = Required(correlationId, 128, nameof(correlationId));
        var document = await GetAsync(documentId, cancellationToken)
            ?? throw new KeyNotFoundException("Document was not found.");
        var version = document.Versions.SingleOrDefault(x => x.Id == versionId)
            ?? throw new KeyNotFoundException("Document version was not found.");
        var path = StoragePath(version.StorageKey);
        if (!File.Exists(path)) throw new FileNotFoundException("Stored document content is missing.");

        await AppendAccessEventAsync(documentId, versionId, actor, correlationId, remoteAddress, cancellationToken);
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return new(document, version, stream);
    }

    private async Task<StoredDocumentVersion> SaveVersionFileAsync(
        Guid documentId,
        int versionNumber,
        Stream source,
        string originalFileName,
        string actor,
        CancellationToken cancellationToken)
    {
        if (source is null || !source.CanRead) throw new ArgumentException("A readable document stream is required.", nameof(source));
        var safeFileName = Path.GetFileName(originalFileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(safeFileName)) throw new ArgumentException("Original file name is required.", nameof(originalFileName));
        if (safeFileName.Length > 260) throw new ArgumentException("File name cannot exceed 260 characters.", nameof(originalFileName));

        var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
        var contentType = ContentTypeFor(extension);
        var versionId = Guid.NewGuid();
        var storageKey = documentId.ToString("N") + "/" + versionId.ToString("N") + ".blob";
        var finalPath = StoragePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
        var tempPath = finalPath + ".tmp-" + Guid.NewGuid().ToString("N");

        try
        {
            long length = 0;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[81920];
                while (true)
                {
                    var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                    if (read == 0) break;
                    length += read;
                    if (length > _maxFileSizeBytes) throw new InvalidDataException($"Document exceeds the {_maxFileSizeBytes / (1024 * 1024)} MB file-size limit.");
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
                await output.FlushAsync(cancellationToken);
            }

            if (length == 0) throw new InvalidDataException("Empty files cannot be stored.");
            await ValidateSignatureAsync(tempPath, extension, cancellationToken);
            File.Move(tempPath, finalPath, overwrite: false);
            return new StoredDocumentVersion(
                versionId,
                versionNumber,
                safeFileName,
                contentType,
                length,
                Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
                storageKey,
                actor,
                DateTimeOffset.UtcNow);
        }
        catch
        {
            TryDeleteFile(tempPath);
            throw;
        }
    }

    private async Task ValidateSignatureAsync(string path, string extension, CancellationToken cancellationToken)
    {
        var header = new byte[8];
        await using (var stream = File.OpenRead(path))
        {
            var read = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
            if (read == 0) throw new InvalidDataException("Empty files cannot be stored.");
        }

        switch (extension)
        {
            case ".pdf":
                if (header.Length < 5 || Encoding.ASCII.GetString(header, 0, 5) != "%PDF-") throw new InvalidDataException("File content does not match a PDF signature.");
                break;
            case ".png":
                if (!header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) throw new InvalidDataException("File content does not match a PNG signature.");
                break;
            case ".jpg":
            case ".jpeg":
                if (header[0] != 0xFF || header[1] != 0xD8 || header[2] != 0xFF) throw new InvalidDataException("File content does not match a JPEG signature.");
                break;
            case ".xlsx":
            case ".docx":
                await ValidateOfficePackageAsync(path, extension, cancellationToken);
                break;
            case ".csv":
            case ".txt":
                await ValidateTextAsync(path, cancellationToken);
                break;
            default:
                throw new InvalidDataException("Unsupported document type. Allowed extensions: .pdf, .xlsx, .docx, .csv, .txt, .png, .jpg, .jpeg.");
        }
    }

    private static Task ValidateOfficePackageAsync(string path, string extension, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var archive = ZipFile.OpenRead(path);
            if (!archive.Entries.Any(x => string.Equals(x.FullName, "[Content_Types].xml", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Office package is missing [Content_Types].xml.");
            var prefix = extension == ".xlsx" ? "xl/" : "word/";
            if (!archive.Entries.Any(x => x.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("File content does not match the selected Office document type.");
        }
        catch (InvalidDataException) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException("Office package could not be validated.", exception);
        }
        return Task.CompletedTask;
    }

    private static async Task ValidateTextAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var buffer = new byte[Math.Min(4096, (int)Math.Min(stream.Length, 4096))];
        var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
        if (buffer.AsSpan(0, read).Contains((byte)0)) throw new InvalidDataException("Text/CSV document contains binary NUL bytes.");
    }

    private async Task WriteMetadataAtomicAsync(StoredDocument document, CancellationToken cancellationToken)
    {
        var path = MetadataPath(document.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        var json = JsonSerializer.Serialize(document, JsonOptions);
        await File.WriteAllTextAsync(temp, json, new UTF8Encoding(false), cancellationToken);
        File.Move(temp, path, overwrite: true);
    }

    private async Task AppendAccessEventAsync(Guid documentId, Guid versionId, string actor, string correlationId, string? remoteAddress, CancellationToken cancellationToken)
    {
        var record = JsonSerializer.Serialize(new
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            VersionId = versionId,
            Action = "Download",
            Actor = actor,
            AccessedAtUtc = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
            RemoteAddress = remoteAddress
        });
        var path = Path.Combine(DocumentDirectory(documentId), "access.ndjson");
        await Gate.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(path, record + Environment.NewLine, new UTF8Encoding(false), cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }

    private string DocumentDirectory(Guid id) => Path.Combine(_root, id.ToString("N"));
    private string MetadataPath(Guid id) => Path.Combine(DocumentDirectory(id), "document.json");

    private string StoragePath(string storageKey)
    {
        var normalized = storageKey.Replace('/', Path.DirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(_root, normalized));
        var rootPrefix = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid document storage key.");
        return path;
    }

    private static string ContentTypeFor(string extension)
        => extension switch
        {
            ".pdf" => "application/pdf",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".csv" => "text/csv",
            ".txt" => "text/plain",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => throw new InvalidDataException("Unsupported document type.")
        };

    private static string Required(string value, int max, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > max) throw new ArgumentException($"Value cannot exceed {max} characters.", parameterName);
        return normalized;
    }

    private static string? Optional(string? value, int max, string parameterName)
        => string.IsNullOrWhiteSpace(value) ? null : Required(value, max, parameterName);

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { }
    }
}
