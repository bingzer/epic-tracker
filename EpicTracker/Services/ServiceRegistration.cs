using EpicTracker.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EpicTracker.Services;

public static class ServiceRegistration
{
    public static IServiceCollection AddEpicTracker(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<EpicTrackerDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<EpicService>();
        services.AddSingleton<TmuxService>();

        return services;
    }
}
