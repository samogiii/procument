using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Procument.Module.Purchasing.Services;

/// <summary>One row of an availability file, as written.</summary>
public sealed record PpParsedRow(
    string PartNo, string? AltPartNo, string Company, string? Comment, string? Qty, string? Condition,
    string? Description, string? Price, string? Phone, string? Fax, string? Email, string? Distance,
    string? Cage, string? Exch);

/// <summary>
/// Reads availability lists — the ILS "Parts Availability Results" CSV (a few title lines, then
/// "ListCode,Item,Company,Supplier Comment,PartNo,Alt PartNo,…") or any sheet with a header row
/// naming at least a part-number and a company column. Excel files arrive as CSV (the browser
/// converts them). The header row is found by its names, so title lines above it are skipped.
/// </summary>
public static class ProcurementPlusFileParser
{
    public const int MaxRows = 20000;
    private static readonly Regex EmailPattern = new(@"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NumberPattern = new(@"\d+(?:\.\d+)?", RegexOptions.Compiled);
    private static readonly Regex NonAlnum = new(@"[^a-z0-9]", RegexOptions.Compiled);

    private enum Col { PartNo, AltPartNo, Company, Comment, Qty, Condition, Description, Price, Phone, Fax, Email, Distance, Cage, Exch }

    // Header names are compared lower-case with everything but letters/digits removed.
    private static readonly Dictionary<string, Col> HeaderNames = new()
    {
        ["partno"] = Col.PartNo, ["partnumber"] = Col.PartNo, ["pn"] = Col.PartNo, ["part"] = Col.PartNo, ["partnum"] = Col.PartNo, ["pno"] = Col.PartNo,
        ["altpartno"] = Col.AltPartNo, ["altpartnumber"] = Col.AltPartNo, ["altpn"] = Col.AltPartNo, ["alternate"] = Col.AltPartNo, ["alt"] = Col.AltPartNo, ["alternatepartnumber"] = Col.AltPartNo,
        ["company"] = Col.Company, ["companyname"] = Col.Company, ["supplier"] = Col.Company, ["suppliername"] = Col.Company, ["vendor"] = Col.Company, ["vendorname"] = Col.Company,
        ["suppliercomment"] = Col.Comment, ["comment"] = Col.Comment, ["comments"] = Col.Comment, ["remark"] = Col.Comment, ["remarks"] = Col.Comment, ["notes"] = Col.Comment,
        ["qty"] = Col.Qty, ["quantity"] = Col.Qty, ["qtyavailable"] = Col.Qty,
        ["cond"] = Col.Condition, ["condition"] = Col.Condition, ["conditioncode"] = Col.Condition, ["cd"] = Col.Condition,
        ["description"] = Col.Description, ["desc"] = Col.Description, ["partdescription"] = Col.Description,
        ["price"] = Col.Price, ["unitprice"] = Col.Price, ["cost"] = Col.Price,
        ["phone"] = Col.Phone, ["tel"] = Col.Phone, ["telephone"] = Col.Phone,
        ["fax"] = Col.Fax,
        ["email"] = Col.Email, ["emailaddress"] = Col.Email, ["mail"] = Col.Email,
        ["distance"] = Col.Distance,
        ["cage"] = Col.Cage, ["cagecode"] = Col.Cage,
        ["exch"] = Col.Exch, ["exchange"] = Col.Exch,
    };

    /// <summary>Rows of the file; throws InvalidOperationException when no header row is found.</summary>
    public static List<PpParsedRow> Parse(string text)
    {
        foreach (var delimiter in new[] { ',', ';', '\t' })
        {
            var table = ReadCsv(text, delimiter);
            var headerIndex = table.FindIndex(r => FindColumns(r) is { } c && c.ContainsKey(Col.PartNo) && c.ContainsKey(Col.Company));
            if (headerIndex < 0) continue;

            var columns = FindColumns(table[headerIndex])!;
            var rows = new List<PpParsedRow>();
            foreach (var cells in table.Skip(headerIndex + 1))
            {
                string? Get(Col c) => columns.TryGetValue(c, out var i) && i < cells.Count ? NullIfBlank(cells[i]) : null;
                var partNo = Get(Col.PartNo);
                if (partNo == null) continue; // blank / trailing ",,,," lines
                var comment = Get(Col.Comment);
                rows.Add(new PpParsedRow(
                    partNo, Get(Col.AltPartNo), Get(Col.Company) ?? "(no company)", comment, Get(Col.Qty), Get(Col.Condition),
                    Get(Col.Description), Get(Col.Price), Get(Col.Phone), Get(Col.Fax),
                    // No email column value: the first address written in the comment ("sales@x.com AOG …").
                    FirstEmail(Get(Col.Email)) ?? FirstEmail(comment),
                    Get(Col.Distance), Get(Col.Cage), Get(Col.Exch)));
                if (rows.Count >= MaxRows) break;
            }
            return rows;
        }
        throw new InvalidOperationException("No header row with a part number column (PartNo) and a company column (Company) was found.");
    }

    /// <summary>Column index per known header, or null when the row names fewer than two known columns.</summary>
    private static Dictionary<Col, int>? FindColumns(List<string> cells)
    {
        var found = new Dictionary<Col, int>();
        for (var i = 0; i < cells.Count; i++)
        {
            var key = NonAlnum.Replace(cells[i].ToLowerInvariant(), "");
            if (HeaderNames.TryGetValue(key, out var col) && !found.ContainsKey(col)) found[col] = i;
        }
        return found.Count >= 2 ? found : null;
    }

    /// <summary>RFC 4180-style: quoted fields, doubled quotes, delimiters and line breaks inside quotes.</summary>
    private static List<List<string>> ReadCsv(string text, char delimiter)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else quoted = false;
                }
                else field.Append(c);
                continue;
            }
            if (c == '"' && field.Length == 0) quoted = true;
            else if (c == delimiter) { row.Add(field.ToString()); field.Clear(); }
            else if (c == '\r' || c == '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(field.ToString()); field.Clear();
                rows.Add(row); row = new List<string>();
            }
            else field.Append(c);
        }
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }

    public static string? FirstEmail(string? text)
    {
        var m = EmailPattern.Match(text ?? "");
        return m.Success ? m.Value.ToLowerInvariant() : null;
    }

    /// <summary>"27" → 27; "RQST" / blank → null.</summary>
    public static double? ParseQty(string? text)
    {
        var m = NumberPattern.Match((text ?? "").Replace(",", ""));
        return m.Success && double.TryParse(m.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    /// <summary>"$1,250.00" → 1250.00; text without a number (e.g. "CALL") → null.</summary>
    public static decimal? ParsePrice(string? text)
    {
        var m = NumberPattern.Match((text ?? "").Replace(",", ""));
        return m.Success && decimal.TryParse(m.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : null;
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
