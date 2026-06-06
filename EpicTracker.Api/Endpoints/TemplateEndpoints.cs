using Carter;
using Microsoft.Extensions.Options;

namespace EpicTracker.Api.Endpoints;

public class TemplateEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/templates/governance", GetGovernanceTemplate);
        app.MapPut("/templates/governance", SaveGovernanceTemplate);
    }

    private static async Task<IResult> GetGovernanceTemplate(
        IOptions<EpicTrackerOptions> options,
        CancellationToken cancellationToken)
    {
        var path = options.Value.GovernanceTemplatePath;

        if (!File.Exists(path))
            return Results.NotFound();

        var content = await File.ReadAllTextAsync(path, cancellationToken);
        return Results.Text(content, "text/plain");
    }

    private static async Task<IResult> SaveGovernanceTemplate(
        SaveGovernanceTemplateRequest request,
        IOptions<EpicTrackerOptions> options,
        CancellationToken cancellationToken)
    {
        var path = options.Value.GovernanceTemplatePath;

        if (!File.Exists(path))
            return Results.NotFound();

        await File.WriteAllTextAsync(path, request.Content, cancellationToken);
        return Results.Ok();
    }
}

public record SaveGovernanceTemplateRequest(string Content);
