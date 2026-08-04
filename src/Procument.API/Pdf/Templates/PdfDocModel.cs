namespace Procument.API.Pdf;

/// <summary>
/// Which visual template a document is rendered with.
/// <list type="bullet">
/// <item><b>Modern</b> — the original coloured layout. Unchanged; still the default.</item>
/// <item><b>Classic</b> — plain black-and-white government/agency form look: boxed sections,
/// hairline rules, uppercase section bars, signature block. No brand colours.</item>
/// <item><b>Standard</b> — halfway house: brand colour used sparingly (title rule, table
/// header text, grand total) over a fully ruled, form-like grid.</item>
/// </list>
/// </summary>
public enum PdfTemplateKind
{
    Modern = 1,
    Classic = 2,
    Standard = 3
}

public static class PdfTemplate
{
    /// <summary>Maps the wire value ("classic", "2", …) onto a template. Unknown ⇒ Modern.</summary>
    public static PdfTemplateKind Resolve(string? raw) => (raw ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "classic" or "simple" or "form" or "plain" or "2" => PdfTemplateKind.Classic,
        "standard" or "balanced" or "middle" or "3" => PdfTemplateKind.Standard,
        _ => PdfTemplateKind.Modern
    };
}

/// <summary>
/// Entry point used by every generator: renders one of the alternative templates, or returns
/// <c>null</c> when the request asks for Modern so the caller falls back to its original layout.
/// The model is built lazily — nothing is mapped when the Modern template is selected.
/// </summary>
public static class PdfTemplateRenderer
{
    public static byte[]? TryRenderAlternate(string? template, Func<PdfDocModel> buildModel)
        => PdfTemplate.Resolve(template) switch
        {
            PdfTemplateKind.Classic => ClassicTemplateRenderer.Render(buildModel()),
            PdfTemplateKind.Standard => StandardTemplateRenderer.Render(buildModel()),
            _ => null
        };
}

/// <summary>
/// Template-neutral description of a business document. Every generator (PO, Proforma Invoice,
/// Final Invoice, Quote) projects its request DTO onto this shape, and the Classic / Standard
/// renderers draw it. Values are already formatted strings so the renderers never re-do business
/// logic — they only decide how it looks.
/// </summary>
public sealed class PdfDocModel
{
    // Identity
    public string DocTitle = "";
    public string? DocNumber;
    /// <summary>Optional bold notice line under the header, e.g. "THIS IS NOT A PURCHASE ORDER".</summary>
    public string? Notice;

    // Company (issuer)
    public string? LogoBase64;
    public string? CompanyName;
    public string? CompanyLocation;
    public string? CompanyPhone;
    public string? CompanyWebsite;
    public string? CompanyEmail;

    // Theme (ignored by Classic, used sparingly by Standard)
    public string Primary = "#1a2744";
    public string Accent = "#2563eb";
    public string CurrencySymbol = "$";

    public List<PdfMetaField> Meta = [];
    public List<PdfAddressBlock> Addresses = [];
    /// <summary>Caption of the section bar above the line-item table.</summary>
    public string ItemsTitle = "Description of Materials or Services";
    public List<PdfTableColumn> Columns = [];
    public List<PdfTableRow> Rows = [];
    public List<PdfInfoBlock> InfoBlocks = [];
    public List<PdfTotalLine> Totals = [];

    public string? Comments;
    public string? Terms;
    public string? FooterText;

    /// <summary>Renders the form-style signature grid at the bottom (Classic only).</summary>
    public bool ShowSignatureBlock = true;
}

public sealed class PdfMetaField
{
    public string Label = "";
    public string? Value;

    public PdfMetaField() { }
    public PdfMetaField(string label, string? value) { Label = label; Value = value; }
}

public sealed class PdfAddressBlock
{
    public string Title = "";
    public string? Name;
    public string? Address;
    public List<PdfMetaField> Fields = [];
    /// <summary>Secondary block printed inside the same box, under a blank line (e.g. FFW under
    /// Ship To). Null — the common case — leaves the box exactly as it was.</summary>
    public PdfAddressBlock? Appended;
}

public enum PdfCellAlign { Left, Center, Right }

public sealed class PdfTableColumn
{
    public string Header = "";
    /// <summary>Fixed width in points. When 0, <see cref="Relative"/> is used instead.</summary>
    public float Width;
    public float Relative;
    public PdfCellAlign Align = PdfCellAlign.Center;

    public static PdfTableColumn Fixed(string header, float width, PdfCellAlign align = PdfCellAlign.Center)
        => new() { Header = header, Width = width, Align = align };

    public static PdfTableColumn Flex(string header, float relative, PdfCellAlign align = PdfCellAlign.Left)
        => new() { Header = header, Relative = relative, Align = align };
}

public sealed class PdfTableRow
{
    public List<PdfCell> Cells = [];
    /// <summary>Extra line printed under the row, spanning the full table width.</summary>
    public string? Note;
}

public sealed class PdfCell
{
    public string Text = "—";
    /// <summary>Second, smaller line inside the same cell — used for "(Alt to: …)".</summary>
    public string? SubText;
    /// <summary>Draw the value with emphasis (bold / accent) — alternate part numbers.</summary>
    public bool Highlight;
    /// <summary>Draw the value as a deduction (red in colour templates).</summary>
    public bool Negative;
    public bool Bold;

    public PdfCell() { }
    public PdfCell(string? text, bool bold = false)
    {
        Text = string.IsNullOrWhiteSpace(text) ? "—" : text!;
        Bold = bold;
    }
}

public sealed class PdfInfoBlock
{
    public string Title = "";
    public List<PdfMetaField> Fields = [];

    /// <summary>True when at least one field carries a value worth printing.</summary>
    public bool HasContent => Fields.Any(f => !string.IsNullOrWhiteSpace(f.Value));
}

public sealed class PdfTotalLine
{
    public string Label = "";
    public decimal Amount;
    /// <summary>The final "Total" row — rendered emphasised.</summary>
    public bool IsGrand;
    /// <summary>Printed with a leading minus (discounts).</summary>
    public bool IsNegative;

    public PdfTotalLine() { }
    public PdfTotalLine(string label, decimal amount, bool isGrand = false, bool isNegative = false)
    {
        Label = label; Amount = amount; IsGrand = isGrand; IsNegative = isNegative;
    }
}
