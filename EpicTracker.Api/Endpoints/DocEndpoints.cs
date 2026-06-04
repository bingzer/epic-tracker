using Carter;

namespace EpicTracker.Api.Endpoints;

public class DocEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/docs", GetDoc);
    }

    private static async Task<IResult> GetDoc(string path, CancellationToken cancellationToken)
    {
        if (!path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest("Only .md files are allowed.");
        }

        if (!File.Exists(path))
        {
            return Results.NotFound();
        }

        var content = await File.ReadAllTextAsync(path, cancellationToken);

        return Results.Text(content, "text/plain");
    }
}
