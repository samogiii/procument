using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Procument.Data;
using Procument.Module.Identity;
using Procument.API.Services;

using Procument.Module.Identity.Services;
using Procument.Module.RFQ;
using Procument.Module.Purchasing;
using Procument.Module.Sales;
using Procument.Shared.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ─── Database ───
// ─── Database ───
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Procument.API.Interceptors.AuditSaveChangesInterceptor>();

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var interceptor = sp.GetRequiredService<Procument.API.Interceptors.AuditSaveChangesInterceptor>();
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.MigrationsAssembly("Procument.Data");
            sqlOptions.CommandTimeout(60);
            sqlOptions.EnableRetryOnFailure(3);
        })
        // NOTE: Tracking stays at the default (TrackAll) because the codebase relies on the
        // "FindAsync(id) → mutate → SaveChanges" pattern in dozens of places. Global NoTracking
        // silently breaks those writes. Read-heavy/list endpoints opt into NoTracking explicitly
        // via .AsNoTracking() or by projecting with .Select(...) to a DTO (projections don't
        // track anyway).
        .AddInterceptors(interceptor);
});


// ─── Modules ───
builder.Services.AddIdentityModule();
builder.Services.AddRFQModule();
builder.Services.AddPurchasingModule();
builder.Services.AddSalesModule();

// ─── Register DbContext base class for module services ───
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());

// ─── Application Services ───
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IFinalInvoiceLockGuard, FinalInvoiceLockGuard>();
builder.Services.AddSingleton<IDocumentStorageService, DocumentStorageService>();

// ─── JWT Authentication ───
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(secretKey)
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin", "SuperAdmin"));
    options.AddPolicy("ExpertOrAdmin", policy => policy.RequireRole("Admin", "SuperAdmin", "Expert"));
});

// ─── Controllers + Audit Filter ───
builder.Services.AddScoped<Procument.API.Filters.AuditActionFilter>();
builder.Services.AddControllers(options =>
{
    options.Filters.AddService<Procument.API.Filters.AuditActionFilter>();
});

// ─── OpenAPI (built-in .NET 10) ───
builder.Services.AddOpenApi();

// ─── CORS ───
builder.Services.AddCors(options =>
{
    // Fully open CORS — allow any origin, header, and method.
    // SetIsOriginAllowed(_ => true) is required (instead of AllowAnyOrigin) when
    // AllowCredentials() is used, because the spec forbids "*" with credentials.
    // Each request's own Origin is echoed back as Access-Control-Allow-Origin.
    options.AddPolicy("AllowClient", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// ─── Seed Data ───
//await DataSeeder.SeedAsync(app.Services);

// ─── Middleware Pipeline ───
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Procument API")
               .WithTheme(ScalarTheme.DeepSpace)
               .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });

    // ─── DANGER: Reset Database Endpoint ───
    // Only available in Development mode for safety
    app.MapPost("/api/dev/reset-database", async (IServiceProvider sp) =>
    {
        try
        {
            await Procument.API.Services.DatabaseResetter.HardResetAndSeedAdminAsync(sp);
            return Results.Ok(new { message = "Database successfully reset and Admin user created." });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to reset database: {ex.Message}");
        }
    })
    .WithName("ResetDatabase")
    .WithOpenApi();
}

// CORS must run BEFORE HttpsRedirection — otherwise a 307 redirect is returned
// without the Access-Control-Allow-Origin header and the browser blocks the request
// (this is what the "No 'Access-Control-Allow-Origin' header is present" error was).
app.UseCors("AllowClient");

// Global exception handler — runs INSIDE CORS so 500 responses still carry the
// Access-Control-Allow-Origin header. Without this, unhandled exceptions short-circuit
// the pipeline before CORS writes headers, and the browser reports a misleading
// "No 'Access-Control-Allow-Origin' header" error instead of the real 500 body.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("GlobalExceptionHandler");
        logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);

        if (!context.Response.HasStarted)
        {
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                error = ex.GetType().Name,
                message = ex.Message,
                // Keep stack only in Development; strip in production if you later want to hide it.
                stack = app.Environment.IsDevelopment() ? ex.ToString() : null,
            });
            await context.Response.WriteAsync(payload);
        }
    }
});

// HTTPS redirect is disabled in development because the client hits the API over plain
// HTTP on the LAN (e.g. http://192.168.37.100:3333). Re-enable behind a reverse proxy
// that terminates TLS, or in non-dev environments.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
