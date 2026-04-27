using Microsoft.Extensions.DependencyInjection;
using Procument.Module.Tasks.Services;

namespace Procument.Module.Tasks;

public static class TasksModule
{
    public static IServiceCollection AddTasksModule(this IServiceCollection services)
    {
        services.AddScoped<ITaskService, TaskService>();
        return services;
    }
}
