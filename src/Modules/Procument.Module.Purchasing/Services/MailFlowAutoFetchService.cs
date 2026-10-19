using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Procument.Module.Purchasing.Services;

/// <summary>
/// Sam (2026-10-19): every 3 minutes Procument asks MailFlow for new mail — a Quote Export run (supplier
/// quotes → Email Quotes) and a CSV Export run (customer RFQs → Email RFQs), the same runs the "Fetch
/// from MailFlow" buttons start. MailFlow then posts what it reads to /api/pending-supplier-quotes/import
/// and /api/pending-rfqs/import.
///
/// No duplicates: MailFlow asks Procument (…/known) before reading each email, and both imports skip a
/// Message-ID they already have — pending, accepted or REMOVED (removed lines are kept as tombstones, so a
/// removed email stays on the Removed tab and never comes back to Pending).
///
/// Runs cover mail received since a day before the last successful start (the first run: the last
/// AutoFetchFirstDays days), all mail not just unread — someone may have opened it in Outlook already.
/// One run per mailbox set to that export, one after another (multiple mailboxes); a run still going when
/// the next tick comes (e.g. one a user started) is simply left to finish.
/// Config: MailFlow:AutoFetchSeconds (default 180 = 3 min, 0 = off), MailFlow:BaseUrl, MailFlow:ApiKey.
/// </summary>
public class MailFlowAutoFetchService : BackgroundService
{
    private const int AutoFetchFirstDays = 1;
    private const int OverlapDays = 1;

    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<MailFlowAutoFetchService> _logger;
    private readonly Dictionary<string, DateTime> _lastStarted = new();
    private readonly Dictionary<string, string> _lastProblem = new();

    public MailFlowAutoFetchService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<MailFlowAutoFetchService> logger)
    {
        _httpFactory = httpFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = _config.GetValue("MailFlow:AutoFetchSeconds", 180);
        if (seconds <= 0 || string.IsNullOrWhiteSpace(_config["MailFlow:BaseUrl"]))
        {
            _logger.LogInformation("MailFlow auto-fetch is off (MailFlow:AutoFetchSeconds / MailFlow:BaseUrl).");
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); // let the API finish starting
        while (!stoppingToken.IsCancellationRequested)
        {
            await FetchAsync("quote-export", "quote", stoppingToken);
            await FetchAsync("csv-export", "rfq", stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
        }
    }

    private const int RunPollSeconds = 5;
    private static readonly TimeSpan MaxRunWait = TimeSpan.FromMinutes(15);

    /// <summary>
    /// One run per MailFlow mailbox set to this export (MailFlow Accounts page: RFQ Export / Quote Export),
    /// one after another — MailFlow runs one export of a kind at a time. No mailbox set to it → MailFlow's
    /// default mailbox. Skipped while a run (e.g. one a user started) is still going.
    /// </summary>
    private async Task FetchAsync(string export, string kind, CancellationToken ct)
    {
        var status = await GetStatusAsync(export, ct);
        if (status == null || status.Value.Running) return;

        var mailboxes = status.Value.Accounts.Where(a => a.Kind == kind).Select(a => (long?)a.Id).ToList();
        if (mailboxes.Count == 0) mailboxes.Add(null);
        foreach (var accountId in mailboxes)
        {
            if (!await StartRunAsync(export, accountId, ct)) continue;
            // Wait for it to finish before the next mailbox (a second start would just get 409).
            var waited = TimeSpan.Zero;
            while (waited < MaxRunWait && !ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(RunPollSeconds), ct);
                waited += TimeSpan.FromSeconds(RunPollSeconds);
                var now = await GetStatusAsync(export, ct);
                if (now == null || !now.Value.Running) break;
            }
        }
    }

    private readonly record struct MailFlowStatus(bool Running, List<(long Id, string? Kind)> Accounts);

    private async Task<MailFlowStatus?> GetStatusAsync(string export, CancellationToken ct)
    {
        var baseUrl = _config["MailFlow:BaseUrl"]!.TrimEnd('/') + "/";
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(baseUrl), $"api/{export}/"));
        request.Headers.Add("X-Api-Key", _config["MailFlow:ApiKey"] ?? string.Empty);
        var client = _httpFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        try
        {
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                Report(export, $"MailFlow answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
                return null;
            }
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            var accounts = new List<(long, string?)>();
            if (root.TryGetProperty("accounts", out var list) && list.ValueKind == JsonValueKind.Array)
                foreach (var a in list.EnumerateArray())
                    if (a.TryGetProperty("id", out var id) && id.TryGetInt64(out var accountId))
                        accounts.Add((accountId, a.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() : null));
            var running = root.TryGetProperty("running", out var r) && r.ValueKind == JsonValueKind.True;
            return new MailFlowStatus(running, accounts);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException && !ct.IsCancellationRequested)
        {
            Report(export, $"MailFlow is not reachable at {baseUrl} ({ex.Message})");
            return null;
        }
    }

    /// <summary>Starts a run for one mailbox (null = MailFlow's default). True when it started.</summary>
    private async Task<bool> StartRunAsync(string export, long? accountId, CancellationToken ct)
    {
        var key = $"{export}:{accountId}";
        var now = DateTime.UtcNow;
        var since = _lastStarted.TryGetValue(key, out var last)
            ? last.AddDays(-OverlapDays)
            : now.AddHours(-AutoFetchFirstDays);
        var body = JsonSerializer.Serialize(new
        {
            accountId,
            mode = "all",
            maxEmails = 0,
            dateFrom = since.ToString("yyyy-MM-dd"),
        });

        var baseUrl = _config["MailFlow:BaseUrl"]!.TrimEnd('/') + "/";
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(baseUrl), $"api/{export}/run/"))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Api-Key", _config["MailFlow:ApiKey"] ?? string.Empty);

        var client = _httpFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        try
        {
            using var response = await client.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                _lastStarted[key] = now;
                Clear(export);
                return true;
            }
            if (response.StatusCode != HttpStatusCode.Conflict) // 409 = a run is still reading
                Report(export, $"MailFlow answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            Report(export, $"MailFlow is not reachable at {baseUrl} ({ex.Message})");
        }
        return false;
    }

    /// <summary>Logs a problem once (not on every run) until it goes away.</summary>
    private void Report(string export, string problem)
    {
        if (_lastProblem.TryGetValue(export, out var previous) && previous == problem) return;
        _lastProblem[export] = problem;
        _logger.LogWarning("MailFlow auto-fetch ({Export}): {Problem}", export, problem);
    }

    private void Clear(string export)
    {
        if (_lastProblem.Remove(export))
            _logger.LogInformation("MailFlow auto-fetch ({Export}) is working again.", export);
    }
}
