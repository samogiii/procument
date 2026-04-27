using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace Procument.Shared.Services;

public interface IDocumentStorageService
{
    string GetRootPath();
    string EnsureProformaInvoiceFolder(string invoiceNumber);
    string EnsureSupplierSubfolder(string invoiceNumber, string supplierName);

    // Category-subfolder operations
    string SaveFileInInvoiceCategory(string invoiceNumber, string categoryFolder, string fileName, Stream content);
    string SaveFileInSupplierCategory(string invoiceNumber, string supplierName, string categoryFolder, string fileName, Stream content);
    IEnumerable<DocumentFileInfo> ListFilesInInvoiceCategories(string invoiceNumber, IEnumerable<(string Key, string Folder)> categories);
    IEnumerable<DocumentFileInfo> ListFilesInSupplierCategories(string invoiceNumber, string supplierName, IEnumerable<(string Key, string Folder)> categories);
    (Stream Stream, string AbsolutePath)? OpenFileInInvoiceCategory(string invoiceNumber, string categoryFolder, string fileName);
    (Stream Stream, string AbsolutePath)? OpenFileInSupplierCategory(string invoiceNumber, string supplierName, string categoryFolder, string fileName);
    bool DeleteFileInInvoiceCategory(string invoiceNumber, string categoryFolder, string fileName);
    bool DeleteFileInSupplierCategory(string invoiceNumber, string supplierName, string categoryFolder, string fileName);

    // Legacy flat-folder operations (kept for backward compatibility)
    string SaveProformaInvoiceFile(string invoiceNumber, string fileName, Stream content);
    string SaveSupplierFile(string invoiceNumber, string supplierName, string fileName, Stream content);
    IEnumerable<DocumentFileInfo> ListProformaInvoiceFiles(string invoiceNumber);
    IEnumerable<DocumentFileInfo> ListSupplierFiles(string invoiceNumber, string supplierName);
    (Stream Stream, string AbsolutePath)? OpenProformaInvoiceFile(string invoiceNumber, string fileName);
    (Stream Stream, string AbsolutePath)? OpenSupplierFile(string invoiceNumber, string supplierName, string fileName);
    bool DeleteProformaInvoiceFile(string invoiceNumber, string fileName);
    bool DeleteSupplierFile(string invoiceNumber, string supplierName, string fileName);

    string Sanitize(string? name);
}

public class DocumentFileInfo
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime ModifiedAt { get; set; }
}

public class DocumentStorageService : IDocumentStorageService
{
    private readonly string _rootPath;

    public DocumentStorageService(IConfiguration configuration)
    {
        var configured = configuration["DocumentStorage:ProformaInvoicesRoot"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            _rootPath = Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(AppContext.BaseDirectory, configured);
        }
        else
        {
            _rootPath = Path.Combine(AppContext.BaseDirectory, "Documents", "ProformaInvoices");
        }

        Directory.CreateDirectory(_rootPath);
    }

    public string GetRootPath() => _rootPath;

    public string EnsureProformaInvoiceFolder(string invoiceNumber)
    {
        var safe = Sanitize(invoiceNumber);
        if (string.IsNullOrEmpty(safe)) throw new ArgumentException("Invalid invoice number", nameof(invoiceNumber));
        var path = Path.Combine(_rootPath, safe);
        Directory.CreateDirectory(path);
        return path;
    }

    public string EnsureSupplierSubfolder(string invoiceNumber, string supplierName)
    {
        var invoiceFolder = EnsureProformaInvoiceFolder(invoiceNumber);
        var safeSupplier = Sanitize(supplierName);
        if (string.IsNullOrEmpty(safeSupplier)) safeSupplier = "Unknown Supplier";
        var path = Path.Combine(invoiceFolder, safeSupplier);
        Directory.CreateDirectory(path);
        return path;
    }

    // ── Category-subfolder operations ──

    public string SaveFileInInvoiceCategory(string invoiceNumber, string categoryFolder, string fileName, Stream content)
    {
        var invoiceDir = EnsureProformaInvoiceFolder(invoiceNumber);
        var catDir = Path.Combine(invoiceDir, categoryFolder);
        Directory.CreateDirectory(catDir);
        var safeName = GetSafeFileName(fileName);
        safeName = ResolveConflict(catDir, safeName);
        using var fs = File.Create(Path.Combine(catDir, safeName));
        content.CopyTo(fs);
        return safeName;
    }

    public string SaveFileInSupplierCategory(string invoiceNumber, string supplierName, string categoryFolder, string fileName, Stream content)
    {
        var supplierDir = EnsureSupplierSubfolder(invoiceNumber, supplierName);
        var catDir = Path.Combine(supplierDir, categoryFolder);
        Directory.CreateDirectory(catDir);
        var safeName = GetSafeFileName(fileName);
        safeName = ResolveConflict(catDir, safeName);
        using var fs = File.Create(Path.Combine(catDir, safeName));
        content.CopyTo(fs);
        return safeName;
    }

    public IEnumerable<DocumentFileInfo> ListFilesInInvoiceCategories(string invoiceNumber, IEnumerable<(string Key, string Folder)> categories)
    {
        var invoiceDir = EnsureProformaInvoiceFolder(invoiceNumber);
        var result = new List<DocumentFileInfo>();
        foreach (var (key, folder) in categories)
        {
            var catDir = Path.Combine(invoiceDir, folder);
            if (!Directory.Exists(catDir)) continue;
            result.AddRange(new DirectoryInfo(catDir)
                .EnumerateFiles()
                .Select(f => new DocumentFileInfo { Name = f.Name, Category = key, Size = f.Length, ModifiedAt = f.LastWriteTimeUtc }));
        }
        return result;
    }

    public IEnumerable<DocumentFileInfo> ListFilesInSupplierCategories(string invoiceNumber, string supplierName, IEnumerable<(string Key, string Folder)> categories)
    {
        var supplierDir = EnsureSupplierSubfolder(invoiceNumber, supplierName);
        var result = new List<DocumentFileInfo>();
        foreach (var (key, folder) in categories)
        {
            var catDir = Path.Combine(supplierDir, folder);
            if (!Directory.Exists(catDir)) continue;
            result.AddRange(new DirectoryInfo(catDir)
                .EnumerateFiles()
                .Select(f => new DocumentFileInfo { Name = f.Name, Category = key, Size = f.Length, ModifiedAt = f.LastWriteTimeUtc }));
        }
        return result;
    }

    public (Stream Stream, string AbsolutePath)? OpenFileInInvoiceCategory(string invoiceNumber, string categoryFolder, string fileName)
    {
        var invoiceDir = EnsureProformaInvoiceFolder(invoiceNumber);
        var path = Path.Combine(invoiceDir, categoryFolder, Sanitize(fileName));
        if (!File.Exists(path)) return null;
        return (File.OpenRead(path), path);
    }

    public (Stream Stream, string AbsolutePath)? OpenFileInSupplierCategory(string invoiceNumber, string supplierName, string categoryFolder, string fileName)
    {
        var supplierDir = EnsureSupplierSubfolder(invoiceNumber, supplierName);
        var path = Path.Combine(supplierDir, categoryFolder, Sanitize(fileName));
        if (!File.Exists(path)) return null;
        return (File.OpenRead(path), path);
    }

    public bool DeleteFileInInvoiceCategory(string invoiceNumber, string categoryFolder, string fileName)
    {
        var invoiceDir = EnsureProformaInvoiceFolder(invoiceNumber);
        var path = Path.Combine(invoiceDir, categoryFolder, Sanitize(fileName));
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    public bool DeleteFileInSupplierCategory(string invoiceNumber, string supplierName, string categoryFolder, string fileName)
    {
        var supplierDir = EnsureSupplierSubfolder(invoiceNumber, supplierName);
        var path = Path.Combine(supplierDir, categoryFolder, Sanitize(fileName));
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    // ── Legacy flat-folder operations ──

    public string SaveProformaInvoiceFile(string invoiceNumber, string fileName, Stream content)
    {
        var folder = EnsureProformaInvoiceFolder(invoiceNumber);
        var safeName = Sanitize(fileName);
        if (string.IsNullOrEmpty(safeName)) throw new ArgumentException("Invalid file name", nameof(fileName));
        var path = Path.Combine(folder, safeName);
        using (var fs = File.Create(path)) { content.CopyTo(fs); }
        return path;
    }

    public string SaveSupplierFile(string invoiceNumber, string supplierName, string fileName, Stream content)
    {
        var folder = EnsureSupplierSubfolder(invoiceNumber, supplierName);
        var safeName = Sanitize(fileName);
        if (string.IsNullOrEmpty(safeName)) throw new ArgumentException("Invalid file name", nameof(fileName));
        var path = Path.Combine(folder, safeName);
        using (var fs = File.Create(path)) { content.CopyTo(fs); }
        return path;
    }

    public IEnumerable<DocumentFileInfo> ListProformaInvoiceFiles(string invoiceNumber)
    {
        var folder = EnsureProformaInvoiceFolder(invoiceNumber);
        return new DirectoryInfo(folder)
            .EnumerateFiles()
            .Select(f => new DocumentFileInfo { Name = f.Name, Size = f.Length, ModifiedAt = f.LastWriteTimeUtc })
            .ToList();
    }

    public IEnumerable<DocumentFileInfo> ListSupplierFiles(string invoiceNumber, string supplierName)
    {
        var folder = EnsureSupplierSubfolder(invoiceNumber, supplierName);
        return new DirectoryInfo(folder)
            .EnumerateFiles()
            .Select(f => new DocumentFileInfo { Name = f.Name, Size = f.Length, ModifiedAt = f.LastWriteTimeUtc })
            .ToList();
    }

    public (Stream Stream, string AbsolutePath)? OpenProformaInvoiceFile(string invoiceNumber, string fileName)
    {
        var folder = EnsureProformaInvoiceFolder(invoiceNumber);
        var path = Path.Combine(folder, Sanitize(fileName));
        if (!File.Exists(path)) return null;
        return (File.OpenRead(path), path);
    }

    public (Stream Stream, string AbsolutePath)? OpenSupplierFile(string invoiceNumber, string supplierName, string fileName)
    {
        var folder = EnsureSupplierSubfolder(invoiceNumber, supplierName);
        var path = Path.Combine(folder, Sanitize(fileName));
        if (!File.Exists(path)) return null;
        return (File.OpenRead(path), path);
    }

    public bool DeleteProformaInvoiceFile(string invoiceNumber, string fileName)
    {
        var folder = EnsureProformaInvoiceFolder(invoiceNumber);
        var path = Path.Combine(folder, Sanitize(fileName));
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    public bool DeleteSupplierFile(string invoiceNumber, string supplierName, string fileName)
    {
        var folder = EnsureSupplierSubfolder(invoiceNumber, supplierName);
        var path = Path.Combine(folder, Sanitize(fileName));
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    public string Sanitize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
        var pattern = "[" + Regex.Escape(invalid) + "]";
        var cleaned = Regex.Replace(name, pattern, "_").Trim();
        cleaned = cleaned.TrimEnd('.', ' ');
        return cleaned;
    }

    // ── Private helpers ──

    private string GetSafeFileName(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        var name = Sanitize(Path.GetFileNameWithoutExtension(fileName));
        if (string.IsNullOrEmpty(name)) name = "file";
        return name + ext;
    }

    private static string ResolveConflict(string directory, string fileName)
    {
        if (!File.Exists(Path.Combine(directory, fileName))) return fileName;
        var nameWithout = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        for (int i = 2; ; i++)
        {
            var candidate = $"{nameWithout} ({i}){ext}";
            if (!File.Exists(Path.Combine(directory, candidate))) return candidate;
        }
    }
}
