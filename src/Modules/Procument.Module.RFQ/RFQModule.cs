using Microsoft.Extensions.DependencyInjection;
using Procument.Module.RFQ.Services;

namespace Procument.Module.RFQ;

public static class RFQModule
{
    public static IServiceCollection AddRFQModule(this IServiceCollection services)
    {
        services.AddScoped<IRFQService, RFQService>();
        services.AddHostedService<RfqAutoExpireService>();
        return services;
    }
}
