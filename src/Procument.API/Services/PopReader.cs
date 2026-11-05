using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace Procument.API.Services;

/// <summary>What the office expects, told to the model so it knows whose fees count (it must still read the POP itself).</summary>
public record PopContext(string Kind, string Text);

/// <summary>The model's answer, cleaned up.</summary>
public record PopRead(
    bool? IsPaymentProof, decimal? Amount, string? Currency, decimal? Fees, string? FeesCurrency, decimal? TotalDebited,
    string? TotalDebitedCurrency, decimal? ReceivedAmount, string? ReceivedCurrency, decimal? ExchangeRate,
    string? ChargesOption, string? ValueDate, string? Reference, decimal? Confidence, string? Note, string RawJson);

/// <summary>
/// Reads a POP with the local vision model (Qwen on llama.cpp's llama-server, OpenAI-style /v1/chat/completions):
/// <c>PopReader:Url</c> (e.g. http://192.168.29.2:7070/v1), <c>PopReader:Model</c>, optional <c>PopReader:ApiKey</c>,
/// <c>PopReader:TimeoutSeconds</c>. Images go as data URLs; PDFs go as their text plus their embedded images (a scanned
/// PDF is just images). Nothing leaves the local network.
/// </summary>
public partial class PopReader(IHttpClientFactory http, IConfiguration config, ILogger<PopReader> log)
{
    private const int MaxPdfPages = 3;
    private const int MaxImages = 4;
    private const long MaxImageBytes = 12 * 1024 * 1024;

    public string? BaseUrl => config["PopReader:Url"]?.Trim().TrimEnd('/') is { Length: > 0 } u ? u : null;
    public string Model => config["PopReader:Model"] is { Length: > 0 } m ? m : "qwen";

    // ── The prompt ───────────────────────────────────────────────────────────

    public const string SystemPrompt = """
        You are a meticulous bank-document reader for a company's finance team. You get ONE proof of payment (POP): a
        photo, scan or screenshot of a bank transfer receipt, a SWIFT MT103 copy, an online-banking confirmation, a bank
        app screen or a credit advice. It may be in English, Chinese or both, rotated, blurry or partly cut off.

        Read the money facts printed on it and answer with ONE JSON object and nothing else: no explanation, no markdown,
        no code fences.

        RULES
        1. Report only what is printed. Never guess, never work out a value that is not shown, never copy the "expected"
           figures from the request. Anything not visible is null.
        2. Numbers are plain JSON numbers with a dot for decimals, no thousands separators, no currency signs:
           "1,234.56" -> 1234.56, "1.234,56" -> 1234.56, "¥ 6,910.00" -> 6910.00, "USD10,000" -> 10000.
           Use Chinese capital amounts (壹仟元整 etc.) only when no digits are shown.
        3. Currencies are ISO codes: ¥ / 元 / 人民币 / RMB / CNH -> "CNY"; $ / 美元 / US$ -> "USD"; € / 欧元 -> "EUR";
           HK$ / 港币 -> "HKD"; £ / 英镑 -> "GBP"; 迪拉姆 -> "AED". A bare symbol takes the currency stated elsewhere on
           the document; if still unclear, null.
        4. transfer_amount = the principal: the amount instructed / sent to the beneficiary (or credited, for incoming
           money), WITHOUT any fee. Labels: Amount, Transfer Amount, Instructed Amount, Remittance Amount, Payment Amount,
           汇款金额, 汇出金额, 转账金额, 交易金额, 金额; SWIFT 32A (currency + amount) or 33B (instructed amount).
        5. fees = every separate charge printed: commission, handling, cable / telex / SWIFT charge, wire fee, postage,
           intermediary or correspondent charges, 手续费, 汇款手续费, 电报费, 邮电费, 中转行费用, SWIFT 71F / 71G.
           One entry per charge. paid_by = "sender" when it was charged to the payer's account (listed with the payer's
           debits, OUR / SHA sender charges), "receiver" when it was taken out of what the beneficiary gets (BEN, 71F
           deducted from the amount, 71G), otherwise "unknown". No fee printed -> [].
        6. total_debited = the total taken from the payer's account including fees, only when printed (Total, Total
           Debit, Debit Amount, Amount Debited, 扣款金额, 实扣金额, 合计, 总计, 总金额). If only one amount is printed it
           is transfer_amount and total_debited stays null.
        7. received_amount / received_currency = the amount credited to the beneficiary, only when printed AND different
           from transfer_amount (another currency, or fees deducted).
        8. exchange_rate = the rate printed on the document (Exchange Rate, Rate, FX Rate, 汇率, 牌价, SWIFT 36), as
           printed. None printed -> null.
        9. charges_option = "OUR", "SHA" or "BEN" when printed (SWIFT 71A, Details of Charges, Charges Borne By; 全额到账
           -> "OUR", 共同承担 -> "SHA", 收款人承担 -> "BEN"), else null.
        10. value_date = YYYY-MM-DD when a date is printed (Value Date, Transaction Date, 交易日期, 日期).
            reference = the bank's reference (Transaction Ref, UETR, 流水号, 业务编号, 交易序号) when printed.
            payer_name / beneficiary_name as printed (汇款人 / 付款人, 收款人), else null.
        11. direction = "outgoing" when the document shows money leaving the account holder, "incoming" when money
            arrived, else "unknown".
        12. is_payment_proof = false only when this is clearly not a bank payment proof (an invoice, a quotation, a chat,
            an empty page).
        13. confidence = 0.0 to 1.0: how sure you are that transfer_amount and transfer_currency are right. Lower it for
            blur, cut-off digits, several candidate amounts or a missing currency.
        14. notes = at most one short sentence about what the office should double-check, or null.

        ANSWER SHAPE (exactly these keys):
        {"is_payment_proof":true,"direction":"outgoing","transfer_amount":1000.00,"transfer_currency":"USD",
         "fees":[{"label":"Commission","amount":25.00,"currency":"USD","paid_by":"sender"}],
         "total_debited":1025.00,"total_debited_currency":"USD","received_amount":null,"received_currency":null,
         "exchange_rate":null,"charges_option":"SHA","value_date":"2026-11-03","reference":"FT2630700123",
         "payer_name":null,"beneficiary_name":null,"confidence":0.95,"notes":null}
        """;

    /// <summary>The kind-specific part: whose money it is and which fees are ours.</summary>
    public static string WithdrawContext(string walletCurrency, decimal amount, string currency, string toName) => $"""
        CONTEXT (orientation only: read the document, do not copy these numbers)
        - This POP is for money WE paid OUT of our own bank account, to {toName}.
        - Our account is in {walletCurrency}. The office asked to send {Money(amount)} {currency}.
        - Every fee our bank charged us counts for us: list each one in "fees" with paid_by "sender". If the document
          shows a total debited, or an amount above {Money(amount)}, report it as printed.
        - Our internal handling fee is never printed on a POP: do not look for it, do not invent it.
        Return the JSON object.
        """;

    public static string TransferContext(string fromCurrency, string toCurrency, decimal amount, decimal? expectedReceived) => $"""
        CONTEXT (orientation only: read the document, do not copy these numbers)
        - This POP is for money WE moved between two of OUR OWN bank accounts: from a {fromCurrency} account to a
          {toCurrency} account.
        - The office asked to send {Money(amount)} {fromCurrency}{(expectedReceived is { } r && !string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase) ? $" (about {Money(r)} {toCurrency} on arrival)" : "")}.
        - transfer_amount = what left the {fromCurrency} account (without fees). Every fee charged on it goes in "fees"
          with its paid_by. If the credited amount in {toCurrency} or a conversion rate is printed, report them in
          received_amount / exchange_rate.
        - Our internal handling fee is never printed on a POP: do not look for it, do not invent it.
        Return the JSON object.
        """;

    public static string DepositContext(decimal amount, string currency, string? customer) => $"""
        CONTEXT (orientation only: read the document, do not copy these numbers)
        - This is OUR bank's proof of money a CUSTOMER{(customer != null ? $" ({customer})" : "")} sent TO us (incoming money).
        - We expect about {Money(amount)} {currency}.
        - transfer_amount = the amount the customer sent / that was credited, as printed.
        - Wire fees and charges on this document were paid by the customer, NOT by us. Still list them in "fees" with
          their paid_by, but never subtract them from transfer_amount and never add them to it.
        Return the JSON object.
        """;

    private static string Money(decimal v) => v.ToString("#,0.00", CultureInfo.InvariantCulture);

    // ── Calling the model ────────────────────────────────────────────────────

    /// <exception cref="PopReaderException">Not configured, unreachable, or no usable answer.</exception>
    public async Task<PopRead> ReadAsync(byte[] file, string fileName, PopContext context, CancellationToken ct)
    {
        var url = BaseUrl ?? throw new PopReaderException("PopReader:Url is not set.");
        var (images, pdfText) = Prepare(file, fileName);
        if (images.Count == 0 && string.IsNullOrWhiteSpace(pdfText))
            throw new PopReaderException("The POP has no image or text the reader can use.");

        var content = new JsonArray();
        var text = new StringBuilder(context.Text);
        if (!string.IsNullOrWhiteSpace(pdfText))
            text.Append("\n\nTEXT OF THE PDF (as extracted, may be out of order):\n").Append(pdfText);
        content.Add(new JsonObject { ["type"] = "text", ["text"] = text.ToString() });
        foreach (var (mime, bytes) in images)
            content.Add(new JsonObject
            {
                ["type"] = "image_url",
                ["image_url"] = new JsonObject { ["url"] = $"data:{mime};base64,{Convert.ToBase64String(bytes)}" },
            });

        var body = new JsonObject
        {
            ["model"] = Model,
            ["temperature"] = 0,
            ["top_p"] = 1,
            ["max_tokens"] = 1500,
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "system", ["content"] = SystemPrompt },
                new JsonObject { ["role"] = "user", ["content"] = content },
            },
            // llama-server: JSON-only output (grammar) and no <think> block on Qwen's thinking models.
            ["response_format"] = new JsonObject { ["type"] = "json_object" },
            ["chat_template_kwargs"] = new JsonObject { ["enable_thinking"] = false },
        };

        var answer = await PostAsync(url, body, ct);
        if (answer == null)
        {
            // An older server that rejects those two options: ask again without them, the prompt still asks for JSON.
            body.Remove("response_format");
            body.Remove("chat_template_kwargs");
            answer = await PostAsync(url, body, ct) ?? throw new PopReaderException("The model server refused the request.");
        }
        return Parse(answer);
    }

    /// <returns>The model's text, or null when the server answered 400 (options it does not know).</returns>
    private async Task<string?> PostAsync(string url, JsonObject body, CancellationToken ct)
    {
        var client = http.CreateClient(nameof(PopReader));
        client.Timeout = TimeSpan.FromSeconds(config.GetValue("PopReader:TimeoutSeconds", 300));
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{url}/chat/completions")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        if (config["PopReader:ApiKey"] is { Length: > 0 } key) request.Headers.Authorization = new("Bearer", key);

        HttpResponseMessage response;
        try { response = await client.SendAsync(request, ct); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new PopReaderException($"Could not reach the model server at {url}: {e.Message}");
        }
        using (response)
        {
            var raw = await response.Content.ReadAsStringAsync(ct);
            if (response.StatusCode == HttpStatusCode.BadRequest && body.ContainsKey("response_format"))
            {
                log.LogInformation("POP reader: server refused response_format / chat_template_kwargs ({Body}); retrying without", Cut(raw, 300));
                return null;
            }
            if (!response.IsSuccessStatusCode)
                throw new PopReaderException($"The model server answered {(int)response.StatusCode}: {Cut(raw, 300)}");
            try
            {
                var node = JsonNode.Parse(raw);
                return node?["choices"]?[0]?["message"]?["content"]?.GetValue<string>()
                    ?? throw new PopReaderException("The model server sent no answer.");
            }
            catch (JsonException)
            {
                throw new PopReaderException("The model server sent something that is not JSON.");
            }
        }
    }

    // ── Files → images / text ────────────────────────────────────────────────

    private (List<(string Mime, byte[] Bytes)> Images, string? PdfText) Prepare(byte[] file, string fileName)
    {
        var images = new List<(string, byte[])>();
        if (IsPdf(file, fileName))
        {
            var text = new StringBuilder();
            try
            {
                using var pdf = PdfDocument.Open(file);
                foreach (var page in pdf.GetPages().Take(MaxPdfPages))
                {
                    var pageText = page.Text;
                    if (!string.IsNullOrWhiteSpace(pageText)) text.AppendLine(pageText.Trim());
                    foreach (var image in page.GetImages())
                    {
                        if (images.Count >= MaxImages) break;
                        if (image.TryGetPng(out var png) && png.Length > 2048) images.Add(("image/png", png));
                        else if (image.RawBytes.ToArray() is { Length: > 2048 } raw && raw[0] == 0xFF && raw[1] == 0xD8) images.Add(("image/jpeg", raw));
                    }
                }
            }
            catch (Exception e)
            {
                log.LogWarning(e, "POP reader: could not open the PDF {Name}", fileName);
                throw new PopReaderException("The PDF could not be opened.");
            }
            var t = text.ToString();
            return (images, t.Length > 12000 ? t[..12000] : t);
        }

        if (file.Length > MaxImageBytes) throw new PopReaderException("The POP image is larger than 12 MB.");
        var mime = ImageMime(file) ?? throw new PopReaderException("The POP is neither an image nor a PDF.");
        images.Add((mime, file));
        return (images, null);
    }

    private static bool IsPdf(byte[] f, string name) =>
        (f.Length > 4 && f[0] == '%' && f[1] == 'P' && f[2] == 'D' && f[3] == 'F') || name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    private static string? ImageMime(byte[] f) =>
        f.Length < 12 ? null
        : f[0] == 0xFF && f[1] == 0xD8 ? "image/jpeg"
        : f[0] == 0x89 && f[1] == 'P' && f[2] == 'N' && f[3] == 'G' ? "image/png"
        : f[0] == 'R' && f[1] == 'I' && f[2] == 'F' && f[3] == 'F' && f[8] == 'W' && f[9] == 'E' && f[10] == 'B' && f[11] == 'P' ? "image/webp"
        : f[0] == 'G' && f[1] == 'I' && f[2] == 'F' ? "image/gif"
        : f[0] == 'B' && f[1] == 'M' ? "image/bmp"
        : null;

    // ── The answer → numbers ─────────────────────────────────────────────────

    public static PopRead Parse(string answer)
    {
        var json = ThinkBlock().Replace(answer, "").Trim();
        var start = json.IndexOf('{');
        var end = json.LastIndexOf('}');
        if (start < 0 || end <= start) throw new PopReaderException($"The model did not answer with JSON: {Cut(answer, 200)}");
        json = json[start..(end + 1)];

        JsonNode root;
        try { root = JsonNode.Parse(json) ?? throw new PopReaderException("The model answered with empty JSON."); }
        catch (JsonException) { throw new PopReaderException($"The model's JSON could not be read: {Cut(json, 200)}"); }

        var currency = Currency(Str(root["transfer_currency"]));
        // Fees we may have paid: the sender's (or unknown) charges in the transfer currency. A deposit ignores them later.
        decimal? fees = null;
        string? feesCurrency = null;
        if (root["fees"] is JsonArray list)
            foreach (var f in list.OfType<JsonObject>())
            {
                var amount = Num(f["amount"]);
                if (amount is not > 0) continue;
                if (string.Equals(Str(f["paid_by"]), "receiver", StringComparison.OrdinalIgnoreCase)) continue;
                var c = Currency(Str(f["currency"])) ?? currency;
                if (feesCurrency != null && c != null && c != feesCurrency) continue;
                feesCurrency ??= c;
                fees = (fees ?? 0) + amount.Value;
            }

        var confidence = Num(root["confidence"]);
        if (confidence is > 1 and <= 100) confidence /= 100;
        return new PopRead(
            root["is_payment_proof"] is JsonValue p && p.TryGetValue<bool>(out var proof) ? proof : null,
            Num(root["transfer_amount"]), currency, fees, feesCurrency,
            Num(root["total_debited"]), Currency(Str(root["total_debited_currency"])) ?? (Num(root["total_debited"]) != null ? currency : null),
            Num(root["received_amount"]), Currency(Str(root["received_currency"])),
            Num(root["exchange_rate"]) is > 0 and var rate ? Math.Round(rate, 6) : null,
            Str(root["charges_option"])?.ToUpperInvariant() is { } option && option is "OUR" or "SHA" or "BEN" ? option : null,
            Cut(Str(root["value_date"]), 50), Cut(Str(root["reference"]), 200),
            confidence is >= 0 and <= 1 ? Math.Round(confidence.Value, 2) : null,
            Cut(Str(root["notes"]), 1000), json);
    }

    private static string? Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s) && s != "null" ? s.Trim() : null;

    /// <summary>A JSON number, or a string the model put a number in ("1,234.56", "¥6910").</summary>
    private static decimal? Num(JsonNode? n)
    {
        if (n is not JsonValue v) return null;
        if (v.TryGetValue<decimal>(out var d)) return d;
        if (v.TryGetValue<double>(out var x)) return (decimal)x;
        var s = Str(n);
        if (s == null) return null;
        s = Regex.Replace(s, @"[^\d.,\-]", "");
        if (s.Contains(',') && s.Contains('.')) s = s.LastIndexOf(',') > s.LastIndexOf('.') ? s.Replace(".", "").Replace(',', '.') : s.Replace(",", "");
        else if (s.Contains(',')) s = Regex.IsMatch(s, @",\d{1,2}$") ? s.Replace(',', '.') : s.Replace(",", "");
        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var r) ? r : null;
    }

    private static string? Currency(string? c)
    {
        var u = c?.Trim().ToUpperInvariant();
        return u switch
        {
            null or "" => null,
            "RMB" or "CNH" or "¥" or "元" or "人民币" => "CNY",
            "$" or "US$" or "美元" => "USD",
            "€" or "欧元" => "EUR",
            "HK$" or "港币" => "HKD",
            "£" or "英镑" => "GBP",
            _ => u.Length <= 10 ? u : null,
        };
    }

    private static string? Cut(string? s, int max) => s == null ? null : s.Length > max ? s[..max] : s;

    [GeneratedRegex(@"<think>.*?</think>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ThinkBlock();
}

public class PopReaderException(string message) : Exception(message);
