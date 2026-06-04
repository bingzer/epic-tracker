using Carter;
using EpicTracker.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace EpicTracker.Api.Endpoints;

public class SpecEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/epics/{epicId}/specs", CreateSpec);
        app.MapGet("/specs/{id}", GetSpec);
        app.MapPut("/specs/{id}", UpdateSpec);
        app.MapPost("/specs/{id}/advance", AdvanceSpec);
        app.MapPost("/specs/{id}/approve-human-in-loop", ApproveHumanInLoop);
        app.MapPost("/specs/{id}/force-state", ForceState);
        app.MapPost("/specs/{id}/ready", MarkReady);
    }

    private static async Task<IResult> CreateSpec(
        string epicId,
        CreateSpecRequest body,
        EpicService service,
        IHubContext<EpicHub> hubContext,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateSpec(epicId, body, cancellationToken);
        await hubContext.Clients.All.SendAsync("SpecUpdated", result, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetSpec(
        string id,
        EpicService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetSpec(id, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateSpec(
        string id,
        Spec spec,
        EpicService service,
        IHubContext<EpicHub> hubContext,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateSpec(spec, cancellationToken);
        await hubContext.Clients.All.SendAsync("SpecUpdated", result, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> AdvanceSpec(
        string id,
        EpicService service,
        IHubContext<EpicHub> hubContext,
        CancellationToken cancellationToken)
    {
        var result = await service.AdvanceSpec(id, cancellationToken);
        await hubContext.Clients.All.SendAsync("SpecUpdated", result, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> ForceState(
        string id,
        ForceSpecStateRequest body,
        EpicService service,
        IHubContext<EpicHub> hubContext,
        CancellationToken cancellationToken)
    {
        var result = await service.ForceSpecState(id, body.StateName, cancellationToken);
        await hubContext.Clients.All.SendAsync("SpecUpdated", result, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> MarkReady(
        string id,
        EpicService service,
        IHubContext<EpicHub> hubContext,
        CancellationToken cancellationToken)
    {
        var result = await service.MarkSpecReadyToCode(id, cancellationToken);
        await hubContext.Clients.All.SendAsync("SpecUpdated", result, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> ApproveHumanInLoop(
        string id,
        ApproveSpecHumanInLoopRequest body,
        EpicService service,
        IHubContext<EpicHub> hubContext,
        CancellationToken cancellationToken)
    {
        await service.ApproveSpecHumanInLoop(id, body, cancellationToken);
        var result = await service.GetSpec(id, cancellationToken);
        await hubContext.Clients.All.SendAsync("SpecUpdated", result, cancellationToken);
        return Results.Ok(result);
    }
}
