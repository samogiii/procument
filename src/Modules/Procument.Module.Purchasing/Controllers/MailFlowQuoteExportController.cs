using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Procument.Module.Identity.Entities;

namespace Procument.Module.Purchasing.Controllers;

/// <summary>
/// Lets the Email Quotes page ask MailFlow to run its Quote Export now, and show its progress.
/// A thin proxy: MailFlow's JSON is passed through as-is, and MailFlow's address and key
/// (MailFlow:BaseUrl / MailFlow:ApiKey in appsettings) never reach the browser. The quotes it
/// reads arrive the usual way — MailFlow posts them to pending supplier quotes.
/// Same access as reviewing those quotes: the "pendingSupplierQuotes" Menu Access feature.
/// </summary>
[ApiController]
[Route("api/mailflow/quote-export")]
[Authorize]
public class MailFlowQuoteExportController : ControllerBase
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly DbContext _db;

    public MailFlowQuoteExportController(IHttpClientFactory httpFactory, IConfiguration config, DbContext db)
    {
        _httpFactory = httpFactory;
        _config = config;
        _db = db;
    }

    private async Task<bool> CanUseAsync()
    {
        var userName = User.Identity?.Name;
        return !string.IsNullOrEmpty(userName) && await _db.Set<MenuPermission>()
            .AnyAsync(p => p.Feature == PendingSupplierQuotesController.Feature && p.UserName == userName);
    }

    /// <summary>Running?, log tail, the quote emails read so far, and MailFlow's active accounts.</summary>
    [HttpGet]
    public Task<IActionResult> Status() => ForwardAsync(HttpMethod.Get, "api/quote-export/", null);

    /// <summary>Start an export: { accountId?, maxEmails?, mode?: "new" | "all", dateFrom?, dateTo? }.</summary>
    [HttpPost("run")]
    public async Task<IActionResult> Run()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        return await ForwardAsync(HttpMethod.Post, "api/quote-export/run/", string.IsNullOrWhiteSpace(body) ? "{}" : body);
    }

    private async Task<IActionResult> ForwardAsync(HttpMethod method, string path, string? jsonBody)
    {
        if (!await CanUseAsync())
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have access to email quotes." });

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
