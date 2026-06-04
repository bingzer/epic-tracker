using EpicTracker.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EpicTracker.Services;

public static class ServiceRegistration
{
    public static IServiceCollection AddEpicTracker(this IServiceCollection services, string connectionString, IConfiguration configuration, string contentRootPath)
    {
        services.AddDbContext<EpicTrackerDbContext>(options =>
            options.UseSqlite(connectionString));

        var epicsBasePath = configuration["EpicTracker:EpicsBasePath"] ?? string.Empty;
        var governanceTemplatePath = Path.Combine(contentRootPath, "Templates", "governance.md");

        services.Configure<EpicTrackerOptions>(o =>
        {
            o.EpicsBasePath = epicsBasePath;
            o.GovernanceTemplatePath = governanceTemplatePath;
        });

        services.AddScoped<EpicService>();
        services.AddSingleton<TmuxService>();
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<EpicScaffolding>();

        return services;
    }
}
