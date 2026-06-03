using Carter;
using EpicTracker.Api.Hubs;
using EpicTracker.Api.Mcp;
using EpicTracker.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/epic-tracker-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

var connectionString = builder.Configuration.GetConnectionString("Default")!;

builder.Services.AddEpicTracker(connectionString);
builder.Services.AddSignalR();
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly(typeof(EpicAgentMcpTools).Assembly);

builder.Services.AddCarter();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EpicTrackerDbContext>();
    await db.Database.MigrateAsync();
}

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok("ok"));

app.MapGroup("/api").MapCarter();
app.MapHub<EpicHub>("/hubs/epic");
app.MapMcp("/mcp");
app.MapFallbackToFile("index.html");

await app.RunAsync();

public partial class Program;
