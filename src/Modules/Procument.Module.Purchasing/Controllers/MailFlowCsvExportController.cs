using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Procument.Module.Identity.Entities;

namespace Procument.Module.Purchasing.Controllers;

/// <summary>
/// Lets the Email RFQs page ask MailFlow to run its CSV Export (RFQ emails) now, and show its progress —
/// the RFQ twin of <see cref="MailFlowQuoteExportController"/>. MailFlow's JSON is passed through as-is;
/// its address and key (MailFlow:BaseUrl / MailFlow:ApiKey) never reach the browser. The RFQs it reads
/// arrive the usual way — MailFlow posts them to /api/pending-rfqs/import.
/// Same access as reviewing them: the "pendingRfqs" Menu Access feature.
/// </summary>
[ApiController]
[Route("api/mailflow/csv-export")]
[Authorize]
public class MailFlowCsvExportController : ControllerBase
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly DbContext _db;

    public MailFlowCsvExportController(IHttpClientFactory httpFactory, IConfiguration config, DbContext db)
    {
        _httpFactory = httpFactory;
        _config = config;
        _db = db;
    }

    private async Task<bool> CanUseAsync()
    {
        var userName = User.Identity?.Name;
        return !string.IsNullOrEmpty(userName) && await _db.Set<MenuPermission>()
            .AnyAsync(p => p.Feature == PendingRfqsController.Feature && p.UserName == userName);
    }

    /// <summary>Running?, log tail, the RFQ emails read so far, and MailFlow's active accounts.</summary>
    [HttpGet]
    public Task<IActionResult> Status() => ForwardAsync(HttpMethod.Get, "api/csv-export/", null);

    /// <summary>Start an export: { accountId?, maxEmails?, mode?: "new" | "all", dateFrom?, dateTo? }.</summary>
    [HttpPost("run")]
    public async Task<IActionResult> Run()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        return await ForwardAsync(HttpMethod.Post, "api/csv-export/run/", string.IsNullOrWhiteSpace(body) ? "{}" : body);
    }

    private async Task<IActionResult> ForwardAsync(HttpMethod method, string path, string? jsonBody)
    {
        if (!await CanUseAsync())
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have access to email RFQs." });

        var baseUrl = _config["MailFlow:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "MailFlow:BaseUrl is not set in appsettings." });

        using var request = new HttpRequestMessage(method, new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), path));
        request.Headers.Add("X-Api-Key", _config["MailFlow:ApiKey"] ?? string.Empty);
        if (jsonBody != null)
            request.Content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");

        var client = _httpFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);
        try
        {
            using var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            // MailFlow refusing our key is a server config problem, not the user's login.
            var status = response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                ? StatusCodes.Status502BadGateway
                : (int)response.StatusCode;
            return new ContentResult { StatusCode = status, Content = content, ContentType = "application/json" };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = $"MailFlow is not reachable at {baseUrl}." });
        }
    }
}
