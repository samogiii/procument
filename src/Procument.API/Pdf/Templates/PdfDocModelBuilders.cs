using System.Globalization;
using Procument.API.Controllers;

namespace Procument.API.Pdf;

/// <summary>
/// Projects each PDF request DTO onto the template-neutral <see cref="PdfDocModel"/>.
/// The numbers produced here mirror exactly what the Modern template prints — the alternative
/// templates only change the presentation, never the figures.
/// </summary>
public static class PdfDocModelBuilders
{
    // ──────────────────────────────────────────────────────
    // PROFORMA INVOICE
    // ──────────────────────────────────────────────────────
    public static PdfDocModel FromInvoice(InvoicePdfRequest req)
    {
        var sym = req.CurrencySymbol ?? "$";
        var items = req.Items ?? [];
        var showDiscount = req.ShowDiscount;

        var m = new PdfDocModel
        {
            DocTitle = "Proforma Invoice",
            DocNumber = req.InvoiceNumber,
            LogoBase64 = req.LogoBase64,
            CompanyName = req.CompanyName,
            CompanyLocation = req.CompanyLocation,
            CompanyPhone = req.CompanyPhone,
            CompanyWebsite = req.CompanyWebsite,
            CompanyEmail = req.CompanyEmail,
            Primary = req.PrimaryColor ?? "#0f766e",
            Accent = req.AccentColor ?? "#10b981",
            CurrencySymbol = sym,
            Comments = req.Comments,
            Terms = req.Terms,
            FooterText = req.FooterText,
            Meta =
            [
                new("Date", req.InvoiceDate),
                new("Customer PO", req.CustomerPONumber),
                new("Currency", req.Currency)
            ]
        };

        m.Addresses.Add(new PdfAddressBlock
        {
            Title = "Bill To",
            Name = string.IsNullOrWhiteSpace(req.CustomerBillToName) ? req.CustomerName : req.CustomerBillToName,
            Address = req.CustomerBillTo,
            Fields =
            [
                new("Contact", req.CustomerContactPerson),
                new("Email", req.CustomerBillToEmail),
                new("Phone", req.CustomerBillToPhone)
            ]
        });
        m.Addresses.Add(new PdfAddressBlock
        {
            Title = "Ship To",
            Name = string.IsNullOrWhiteSpace(req.CustomerShipToName) ? req.CustomerName : req.CustomerShipToName,
            Address = req.CustomerShipTo,
            Fields =
            [
                new("Contact", req.CustomerShipToContactPerson),
                new("Email", req.CustomerShipToEmail),
                new("Phone", req.CustomerShipToPhone),
                new("Account", req.CustomerShipToAccount)
            ]
        });

        m.Columns =
        [
            PdfTableColumn.Fixed("#", 20),
            PdfTableColumn.Flex("Part No.", 2.2f, PdfCellAlign.Center),
            PdfTableColumn.Flex("Description", 2.2f),
            PdfTableColumn.Fixed("Qty", 26),
            PdfTableColumn.Fixed("CD", 26),
            PdfTableColumn.Fixed("Cert", 55),
            PdfTableColumn.Fixed("Unit Price", 55, PdfCellAlign.Right),
            PdfTableColumn.Fixed("Total", 58, PdfCellAlign.Right)
        ];
        if (showDiscount) m.Columns.Add(PdfTableColumn.Fixed("Discount", 55, PdfCellAlign.Right));
        m.Columns.Add(PdfTableColumn.Fixed("Delivery", 58));

        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            var hasDiscount = it.Discount is > 0;
            var row = new PdfTableRow
            {
                Cells =
                [
                    new PdfCell((i + 1).ToString()),
                    PartNumberCell(it.PartNumberName, it.Alt),
                    new PdfCell(it.Description),
                    new PdfCell(it.Qty.ToString(), bold: true),
                    new PdfCell(it.Condition),
                    new PdfCell(it.CertName),
                    new PdfCell(Money(sym, it.UnitPrice)),
                    new PdfCell(Money(sym, it.TotalPrice), bold: true)
                ]
            };
            if (showDiscount)
                row.Cells.Add(new PdfCell(hasDiscount ? $"-{Money(sym, it.Discount!.Value)}" : "—")
                { Negative = hasDiscount });
            row.Cells.Add(new PdfCell(it.LeadTime));
            m.Rows.Add(row);
        }

        m.InfoBlocks.Add(BankBlock(req.BeneficiaryName, req.BeneficiaryAddress, req.BankName,
            req.BankAddress, req.BankAccount, req.SwiftCode));

        // Same arithmetic as PdfHelpers.DrawTotals in the Modern template:
        // the subtotal is shown gross, with the per-line discounts deducted on their own row.
        var totalDiscount = items.Where(i => i.Discount is > 0).Sum(i => i.Discount!.Value);
        var subtotal = (req.Subtotal ?? 0) + totalDiscount;
        var tax = req.Tax ?? 0;
        var shipping = req.Shipping ?? 0;
        var processingFee = req.Other ?? 0;

        m.Totals.Add(new PdfTotalLine("Subtotal", subtotal));
        if (totalDiscount > 0) m.Totals.Add(new PdfTotalLine("Discount", totalDiscount, isNegative: true));
        m.Totals.Add(new PdfTotalLine(TaxLabel(req.TaxPercent), tax));
        m.Totals.Add(new PdfTotalLine("Shipping", shipping));
        m.Totals.Add(new PdfTotalLine("Processing Fee", processingFee));
        m.Totals.Add(new PdfTotalLine("Total", subtotal - totalDiscount + tax + shipping + processingFee, isGrand: true));

        return m;
    }

    // ──────────────────────────────────────────────────────
    // PURCHASE ORDER
    // ──────────────────────────────────────────────────────
    public static PdfDocModel FromPurchaseOrder(PurchaseOrderPdfRequest req)
    {
        var sym = req.CurrencySymbol ?? "$";
        var items = req.Items ?? [];

        var m = new PdfDocModel
        {
            DocTitle = "Purchase Order",
            DocNumber = req.PoNumber,
            LogoBase64 = req.LogoBase64,
            CompanyName = req.CompanyName,
            CompanyLocation = req.CompanyLocation,
            CompanyPhone = req.CompanyPhone,
            CompanyWebsite = req.CompanyWebsite,
            CompanyEmail = req.CompanyEmail,
            Primary = req.PrimaryColor ?? "#92400e",
            Accent = req.AccentColor ?? "#d97706",
            CurrencySymbol = sym,
            Comments = req.Comments,
            Terms = req.Terms,
            FooterText = req.FooterText,
            Meta =
            [
                new("Date", req.PoDate),
                new("Currency", req.Currency)
            ]
        };

        m.Addresses.Add(ContactBlock("Purchase From", req.PurchaseFromName, req.PurchaseFromAddress,
            req.PurchaseFromPhone, req.PurchaseFromEmail));
        m.Addresses.Add(ContactBlock("Bill To", req.VendorName, req.VendorAddress,
            req.VendorPhone, req.VendorEmail));
        var shipTo = ContactBlock("Ship To", req.DeliverToName, req.DeliverToAddress,
            req.DeliverToPhone, req.DeliverToEmail);
        // FFW rides inside the Ship To box so the columns stay the same width.
        if (PurchaseOrderDocument.HasFfw(req))
            shipTo.Appended = ContactBlock("FFW", req.FfwName, req.FfwAddress, req.FfwPhone, req.FfwEmail);
        m.Addresses.Add(shipTo);

        m.Columns =
        [
            PdfTableColumn.Fixed("#", 22),
            PdfTableColumn.Flex("Part No.", 2f, PdfCellAlign.Center),
            PdfTableColumn.Flex("Description", 2.5f),
            PdfTableColumn.Fixed("Qty", 28),
            PdfTableColumn.Fixed("CD", 28),
            PdfTableColumn.Fixed("Cert", 60),
            PdfTableColumn.Fixed("Buy Price", 60, PdfCellAlign.Right),
            PdfTableColumn.Fixed("Amount", 65, PdfCellAlign.Right),
            PdfTableColumn.Flex("Note", 1.5f)
        ];

        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            m.Rows.Add(new PdfTableRow
            {
                Cells =
                [
                    new PdfCell((i + 1).ToString()),
                    new PdfCell(it.PartNumber, bold: true),
                    new PdfCell(it.Description),
                    new PdfCell(it.Qty.ToString(), bold: true),
                    new PdfCell(it.Condition),
                    new PdfCell(it.Certification),
                    new PdfCell(Money(sym, it.UnitPrice)),
                    new PdfCell(Money(sym, it.TotalPrice), bold: true),
                    new PdfCell(it.Note)
                ]
            });
        }

        m.InfoBlocks.Add(new PdfInfoBlock
        {
            Title = "FedEx Account Information",
            Fields =
            [
                new("Account Number", req.FedExAccount),
                new("Service Priority", req.ServicePriority)
            ]
        });
        m.InfoBlocks.Add(new PdfInfoBlock
        {
            Title = "Shipping Information",
            Fields =
            [
                new("Shipping Method", req.ShippingMethod),
                new("Incoterms", req.Incoterms)
            ]
        });

        var subtotal = req.Subtotal ?? 0;
        var tax = req.Tax ?? 0;
        var shipping = req.TotalShipping ?? 0;
        var processingFee = req.ProcessingFee ?? 0;

        m.Totals.Add(new PdfTotalLine("Subtotal", subtotal));
        m.Totals.Add(new PdfTotalLine("Tax", tax));
        m.Totals.Add(new PdfTotalLine("Shipping", shipping));
        m.Totals.Add(new PdfTotalLine("Processing Fee", processingFee));
        m.Totals.Add(new PdfTotalLine("Total", subtotal + tax + shipping + processingFee, isGrand: true));

        return m;
    }

    // ──────────────────────────────────────────────────────
    // FINAL INVOICE
    // ──────────────────────────────────────────────────────
    public static PdfDocModel FromFinalInvoice(FinalInvoicePdfRequest req)
    {
        var sym = req.CurrencySymbol ?? "$";
        var items = req.Items ?? [];

        var m = new PdfDocModel
        {
            DocTitle = "Invoice",
            DocNumber = req.InvoiceNumber,
            LogoBase64 = req.LogoBase64,
            CompanyName = req.CompanyName,
            CompanyLocation = req.CompanyLocation,
            CompanyPhone = req.CompanyPhone,
            CompanyWebsite = req.CompanyWebsite,
            CompanyEmail = req.CompanyEmail,
            Primary = req.PrimaryColor ?? "#312e81",
            Accent = req.AccentColor ?? "#6366f1",
            CurrencySymbol = sym,
            Comments = req.Comments,
            Terms = req.Terms,
            FooterText = req.FooterText,
            Meta =
            [
                new("Date", req.InvoiceDate),
                new("Customer PO", req.CustomerPONumber),
                new("PI Ref", req.ProformaRef),
                new("Currency", req.Currency)
            ]
        };

        m.Addresses.Add(new PdfAddressBlock
        {
            Title = "Bill To",
            Name = req.CustomerBillToName ?? req.CustomerName,
            Address = req.CustomerBillTo,
            Fields =
            [
                new("Contact Person", req.CustomerBillToContactPerson),
                new("Email", req.CustomerBillToEmail),
                new("Phone", req.CustomerBillToPhone)
            ]
        });
        m.Addresses.Add(new PdfAddressBlock
        {
            Title = "Ship To",
            Name = req.CustomerShipToName ?? req.CustomerName,
            Address = req.CustomerShipTo,
            Fields =
            [
                new("Contact Person", req.CustomerShipToContactPerson),
                new("Email", req.CustomerShipToEmail),
                new("Phone", req.CustomerShipToPhone),
                new("Account", req.CustomerShipToAccount)
            ]
        });

        m.Columns =
        [
            PdfTableColumn.Fixed("#", 18),
            PdfTableColumn.Flex("Part No.", 1.6f, PdfCellAlign.Center),
            PdfTableColumn.Flex("Description", 1.8f),
            PdfTableColumn.Fixed("Qty", 24),
            PdfTableColumn.Fixed("CD", 24),
            PdfTableColumn.Fixed("Cert", 46),
            PdfTableColumn.Fixed("Unit Price", 50, PdfCellAlign.Right),
            PdfTableColumn.Fixed("Total", 54, PdfCellAlign.Right),
            PdfTableColumn.Fixed("Discount", 50, PdfCellAlign.Right),
            PdfTableColumn.Flex("Track #", 1.1f, PdfCellAlign.Center),
            PdfTableColumn.Flex("Carrier", 1.1f, PdfCellAlign.Center)
        ];

        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            var hasDiscount = it.Discount.HasValue;
            m.Rows.Add(new PdfTableRow
            {
                Cells =
                [
                    new PdfCell((i + 1).ToString()),
                    PartNumberCell(it.PartNumber, it.Alt),
                    new PdfCell(it.Description),
                    new PdfCell(it.Qty.ToString(), bold: true),
                    new PdfCell(it.Condition),
                    new PdfCell(it.Certification),
                    new PdfCell(Money(sym, it.UnitPrice)),
                    new PdfCell(Money(sym, it.TotalPrice), bold: true),
                    new PdfCell(hasDiscount ? $"-{Money(sym, it.Discount!.Value)}" : "—") { Negative = hasDiscount },
                    new PdfCell(it.TrackNumber),
                    new PdfCell(it.Carrier)
                ]
            });
        }

        m.InfoBlocks.Add(BankBlock(req.BeneficiaryName, req.BeneficiaryAddress, req.BankName,
            req.BankAddress, req.BankAccount, req.SwiftCode));

        var totalDiscount = items.Where(i => i.Discount.HasValue).Sum(i => i.Discount!.Value);
        var subtotal = req.Subtotal ?? 0;
        var tax = req.Tax ?? 0;
        var shipping = req.ShippingCost ?? 0;
        var processingFee = req.Other ?? 0;

        m.Totals.Add(new PdfTotalLine("Subtotal", subtotal));
        if (totalDiscount > 0) m.Totals.Add(new PdfTotalLine("Discount", totalDiscount, isNegative: true));
        m.Totals.Add(new PdfTotalLine(TaxLabel(req.TaxPercent), tax));
        m.Totals.Add(new PdfTotalLine("Shipping", shipping));
        m.Totals.Add(new PdfTotalLine("Processing Fee", processingFee));
        m.Totals.Add(new PdfTotalLine("Total", subtotal - totalDiscount + tax + shipping + processingFee, isGrand: true));

        return m;
    }

    // ──────────────────────────────────────────────────────
    // QUOTATION
    // ──────────────────────────────────────────────────────
    public static PdfDocModel FromQuote(QuotePdfRequest req)
    {
        var sym = req.CurrencySymbol ?? "$";
        var rate = req.ExchangeRate ?? 1;
        var items = req.Items ?? [];

        var m = new PdfDocModel
        {
            DocTitle = req.DocTitle ?? "Quotation",
            DocNumber = req.QuoteNumber,
            LogoBase64 = req.LogoBase64,
            CompanyName = req.CompanyName,
            CompanyLocation = req.CompanyLocation,
            CompanyPhone = req.CompanyPhone,
            CompanyWebsite = req.CompanyWebsite,
            CompanyEmail = req.CompanyEmail,
            Primary = req.PrimaryColor ?? "#1a2744",
            Accent = req.AccentColor ?? "#2563eb",
            CurrencySymbol = sym,
            Comments = req.Comments,
            Terms = req.Terms,
            FooterText = req.FooterText,
            Meta =
            [
                new("Date", req.QuoteDate),
                new("Valid Until", req.ValidUntil),
                new("RFQ", req.RfqName),
                new("Currency", req.Currency)
            ]
        };

        m.Addresses.Add(new PdfAddressBlock
        {
            Title = "Bill To",
            Name = req.CustomerName,
            Address = req.CustomerBillTo
        });
        m.Addresses.Add(new PdfAddressBlock
        {
            Title = "Ship To",
            Name = req.CustomerName,
            Address = req.CustomerShipTo ?? req.CustomerBillTo
        });

        m.Columns =
        [
            PdfTableColumn.Fixed("#", 22),
            PdfTableColumn.Fixed("Ref", 30),
            PdfTableColumn.Flex("Part No.", 3f, PdfCellAlign.Center),
            PdfTableColumn.Flex("Description", 2.5f),
            PdfTableColumn.Fixed("Qty", 30),
            PdfTableColumn.Fixed("CD", 30),
            PdfTableColumn.Fixed("Lead Time", 55),
            PdfTableColumn.Fixed("Unit Price", 60, PdfCellAlign.Right),
            PdfTableColumn.Fixed("Total", 65, PdfCellAlign.Right)
        ];

        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];

            // The Modern template prints Cert / Tag Date / Note on a sub-row under each item.
            var details = new List<string>();
            if (!string.IsNullOrWhiteSpace(it.CertName)) details.Add($"Cert: {it.CertName}");
            if (!string.IsNullOrWhiteSpace(it.TagDate))
                details.Add($"Tag Date: {(it.TagDate.Length >= 4 ? it.TagDate[..4] : it.TagDate)}");
            if (!string.IsNullOrWhiteSpace(it.Note)) details.Add($"Note: {it.Note}");

            m.Rows.Add(new PdfTableRow
            {
                Note = details.Count > 0 ? string.Join("   ", details) : null,
                Cells =
                [
                    new PdfCell((i + 1).ToString()),
                    new PdfCell(it.RfqReference),
                    PartNumberCell(it.PartNumberName, it.Alt),
                    new PdfCell(it.Description),
                    new PdfCell(it.Qty.ToString(), bold: true),
                    new PdfCell(it.Condition),
                    new PdfCell(it.LeadTime),
                    new PdfCell(Money(sym, it.UnitPrice * rate)),
                    new PdfCell(Money(sym, it.TotalPrice * rate), bold: true)
                ]
            });
        }

        var subtotal = (req.Subtotal ?? 0) * rate;
        var tax = (req.Tax ?? 0) * rate;
        var shipping = (req.Shipping ?? 0) * rate;
        var other = (req.Other ?? 0) * rate;

        m.Totals.Add(new PdfTotalLine("Subtotal", subtotal));
        m.Totals.Add(new PdfTotalLine("Tax", tax));
        m.Totals.Add(new PdfTotalLine("Shipping", shipping));
        m.Totals.Add(new PdfTotalLine("Other", other));
        m.Totals.Add(new PdfTotalLine("Total", subtotal + tax + shipping + other, isGrand: true));

        return m;
    }

    // ──────────────────────────────────────────────────────
    // SHARED
    // ──────────────────────────────────────────────────────
    private static string Money(string sym, decimal value) => $"{sym}{PdfHelpers.FormatPrice(value)}";

    private static string TaxLabel(decimal? taxPercent) => taxPercent is > 0
        ? $"Tax ({taxPercent.Value.ToString("0.##", CultureInfo.InvariantCulture)}%)"
        : "Tax";

    /// <summary>Alt part numbers become the effective PN, with the original kept as a reference line.</summary>
    private static PdfCell PartNumberCell(string? partNumber, string? alt)
    {
        var isAlt = !string.IsNullOrWhiteSpace(alt);
        return new PdfCell(isAlt ? alt : partNumber, bold: true)
        {
            SubText = isAlt ? $"(Alt to: {partNumber})" : null,
            Highlight = isAlt
        };
    }

    private static PdfAddressBlock ContactBlock(string title, string? name, string? address, string? phone, string? email)
        => new()
        {
            Title = title,
            Name = name,
            Address = address,
            Fields = [new("Tel", phone), new("Email", email)]
        };

    private static PdfInfoBlock BankBlock(string? beneficiaryName, string? beneficiaryAddress, string? bankName,
        string? bankAddress, string? account, string? swift)
        => new()
        {
            Title = "Bank Information",
            Fields =
            [
                new("Beneficiary Name", beneficiaryName),
                new("Beneficiary Address", beneficiaryAddress),
                new("Bank Name", bankName),
                new("Bank Address", bankAddress),
                new("Account Number", account),
                new("SWIFT Code", swift)
            ]
        };
}
