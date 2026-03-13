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
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("ExpertOrAdmin", policy => policy.RequireRole("Admin", "Expert"));
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
    options.AddPolicy("AllowClient", policy =>
    {
        policy.WithOrigins("http://192.168.37.100:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
        policy.WithOrigins("http://localhost:3000")
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

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors("AllowClient");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
