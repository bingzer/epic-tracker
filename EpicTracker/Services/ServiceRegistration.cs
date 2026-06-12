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
        var tmuxBrokerUrl = configuration["EpicTracker:TmuxBrokerUrl"] ?? "http://127.0.0.1:6789";

        services.Configure<EpicTrackerOptions>(o =>
        {
            o.EpicsBasePath = epicsBasePath;
            o.GovernanceTemplatePath = governanceTemplatePath;
            o.TmuxBrokerUrl = tmuxBrokerUrl;
        });

        services.AddScoped<EpicService>();
        services.AddHttpClient<BrokerService>();
        services.AddSingleton<TmuxService>();
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<EpicScaffolding>();

        return services;
    }
}
