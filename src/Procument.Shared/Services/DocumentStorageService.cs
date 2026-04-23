using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace Procument.Shared.Services;

public interface IDocumentStorageService
{
    /// <summary>Root directory where all document folders live.</summary>
    string GetRootPath();

    /// <summary>Returns absolute path to the Proforma Invoice folder (creating it if missing).</summary>
    string EnsureProformaInvoiceFolder(string invoiceNumber);

    /// <summary>Returns absolute path to a Supplier subfolder inside a Proforma Invoice folder (creating both if missing).</summary>
    string EnsureSupplierSubfolder(string invoiceNumber, string supplierName);

    /// <summary>Save bytes to the PI folder using the given file name (overwrites).</summary>
    string SaveProformaInvoiceFile(string invoiceNumber, string fileName, Stream content);

    /// <summary>Save bytes to the PI/Supplier folder using the given file name (overwrites).</summary>
    string SaveSupplierFile(string invoiceNumber, string supplierName, string fileName, Stream content);

    /// <summary>List all files directly in the PI folder (not inside supplier subfolders).</summary>
    IEnumerable<DocumentFileInfo> ListProformaInvoiceFiles(string invoiceNumber);

    /// <summary>List all files in a PI/Supplier folder.</summary>
    IEnumerable<DocumentFileInfo> ListSupplierFiles(string invoiceNumber, string supplierName);

    /// <summary>Open a stream to read a PI-level file. Returns null if missing.</summary>
    (Stream Stream, string AbsolutePath)? OpenProformaInvoiceFile(string invoiceNumber, string fileName);

    /// <summary>Open a stream to read a Supplier-level file. Returns null if missing.</summary>
    (Stream Stream, string AbsolutePath)? OpenSupplierFile(string invoiceNumber, string supplierName, string fileName);

    /// <summary>Delete a PI-level file. Returns true if deleted.</summary>
    bool DeleteProformaInvoiceFile(string invoiceNumber, string fileName);

    /// <summary>Delete a Supplier-level file. Returns true if deleted.</summary>
    bool DeleteSupplierFile(string invoiceNumber, string supplierName, string fileName);

    /// <summary>Sanitize a name for safe folder/file usage.</summary>
    string Sanitize(string? name);
}

public class DocumentFileInfo
{
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime ModifiedAt { get; set; }
}

public class DocumentStorageService : IDocumentStorageService
{
    private readonly string _rootPath;

    public DocumentStorageService(IConfiguration configuration)
    {
        // Resolve root path from configuration (fallback: <app base dir>/Documents/ProformaInvoices)
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

    public string SaveProformaInvoiceFile(string invoiceNumber, string fileName, Stream content)
    {
        var folder = EnsureProformaInvoiceFolder(invoiceNumber);
        var safeName = Sanitize(fileName);
        if (string.IsNullOrEmpty(safeName)) throw new ArgumentException("Invalid file name", nameof(fileName));
        var path = Path.Combine(folder, safeName);
        using (var fs = File.Create(path))
        {
            content.CopyTo(fs);
        }
        return path;
    }

    public string SaveSupplierFile(string invoiceNumber, string supplierName, string fileName, Stream content)
    {
        var folder = EnsureSupplierSubfolder(invoiceNumber, supplierName);
        var safeName = Sanitize(fileName);
        if (string.IsNullOrEmpty(safeName)) throw new ArgumentException("Invalid file name", nameof(fileName));
        var path = Path.Combine(folder, safeName);
        using (var fs = File.Create(path))
        {
            content.CopyTo(fs);
        }
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
        var safeName = Sanitize(fileName);
        var path = Path.Combine(folder, safeName);
        if (!File.Exists(path)) return null;
        return (File.OpenRead(path), path);
    }

    public (Stream Stream, string AbsolutePath)? OpenSupplierFile(string invoiceNumber, string supplierName, string fileName)
    {
        var folder = EnsureSupplierSubfolder(invoiceNumber, supplierName);
        var safeName = Sanitize(fileName);
        var path = Path.Combine(folder, safeName);
        if (!File.Exists(path)) return null;
        return (File.OpenRead(path), path);
    }

    public bool DeleteProformaInvoiceFile(string invoiceNumber, string fileName)
    {
        var folder = EnsureProformaInvoiceFolder(invoiceNumber);
        var safeName = Sanitize(fileName);
        var path = Path.Combine(folder, safeName);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    public bool DeleteSupplierFile(string invoiceNumber, string supplierName, string fileName)
    {
        var folder = EnsureSupplierSubfolder(invoiceNumber, supplierName);
        var safeName = Sanitize(fileName);
        var path = Path.Combine(folder, safeName);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    /// <summary>Remove characters that are invalid in a Windows/Linux folder name.</summary>
    public string Sanitize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
        var pattern = "[" + Regex.Escape(invalid) + "]";
        var cleaned = Regex.Replace(name, pattern, "_").Trim();
        // Prevent ".", "..", and trailing dots/spaces that Windows rejects
        cleaned = cleaned.TrimEnd('.', ' ');
        return cleaned;
    }
}
