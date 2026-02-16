using Microsoft.Extensions.DependencyInjection;
using Procument.Module.Identity.Services;

namespace Procument.Module.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IAuditService, AuditService>();
        return services;
    }
}
