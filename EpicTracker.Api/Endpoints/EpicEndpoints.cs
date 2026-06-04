using Carter;
using EpicTracker.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace EpicTracker.Api.Endpoints;

public class EpicEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/epics", ListEpics);
        app.MapPost("/epics", CreateEpic);
        app.MapGet("/epics/{id}", GetEpic);
        app.MapGet("/epics/{id}/history", GetEpicHistory);
        app.MapPut("/epics/{id}", UpdateEpic);
        app.MapPost("/epics/{id}/advance", Advance);
        app.MapPost("/epics/{id}/approve-human-in-loop", ApproveHumanInLoop);
        app.MapPost("/epics/{id}/force-state", ForceState);
        app.MapPost("/epics/{id}/raise-agent-swarm", RaiseAgentSwarm);
        app.MapPost("/epics/{id}/raise-human-in-loop", RaiseHumanInLoop);
        app.MapPost("/epics/{id}/submit-agreement", SubmitAgreement);
        app.MapPost("/epics/{id}/wake-agent", WakeAgent);
        app.MapDelete("/epics/{id}", DeleteEpic);
    }

    private static async Task<IResult> ListEpics(
        EpicService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListEpics(cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateEpic(
        CreateEpicRequest request,
        EpicService service,
        IHubContext<EpicHub> hubContext,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateEpic(request, cancellationToken);
        await hubContext.Clients.All.SendAsync("EpicUpdated", result, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetEpic(
        string id,
        EpicService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetEpic(id, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetEpicHistory(
        string id,
        EpicService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetEpicHistory(id, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateEpic(
        string id,
        Epic epic,
        EpicService service,
        IHubContext<EpicHub> hubContext,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateEpic(epic, cancellationToken);
        await hubContext.Clients.All.SendAsync("EpicUpdated", result, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> Advance(
        string id,
        AdvanceEpicRequest body,
        EpicService service,
        IHubContext<EpicHub> hubContext,
        CancellationToken cancellationToken)
    {
        var result = await service.Advance(id, body, cancellationToken);
        await hubContext.Clients.All.SendAsync("EpicUpdated", result, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> ApproveHumanInLoop(
        string id,
        ApproveEpicHumanInLoopRequest body,
        EpicService service,
        IHubContext<EpicHub> hubContext,
        CancellationToken cancellationToken)
    {
        await service.ApproveHumanInLoop(id, body, cancellationToken);
        var result = await service.GetEpic(id, cancellationToken);
        await hubContext.Clients.All.SendAsync("EpicUpdated", result, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> ForceState(
        string id,
        ForceEpicStateRequest body,
        EpicService service,
        IHubContext<EpicHub> hubContext,
        CancellationToken cancellationToken)
    {
        var result = await service.ForceEpicState(id, body.StateName, cancellationToken);
        await hubContext.Clients.All.SendAsync("EpicUpdated", result, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> RaiseAgentSwarm(
        string id,
        RaiseAgentSwarmRequest body,
        EpicService service,
        IHubContext<EpicHub> hubContext,
        CancellationToken cancellationToken)
    {
        await service.RaiseAgentSwarm(id, body, cancellationToken);
        var result = await service.GetEpic(id, cancellationToken);
        await hubContext.Clients.All.SendAsync("EpicUpdated", result, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> RaiseHumanInLoop(
        string id,
        RaiseHumanInLoopRequest body,
        EpicService service,
        IHubContext<EpicHub> hubContext,
        CancellationToken cancellationToken)
    {
        await service.RaiseHumanInLoop(id, body, cancellationToken);
        var result = await service.GetEpic(id, cancellationToken);
        await hubContext.Clients.All.SendAsync("EpicUpdated", result, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> WakeAgent(
        string id,
        EpicService service,
        CancellationToken cancellationToken)
    {
        await service.WakeAgent(id, cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> DeleteEpic(
        string id,
        EpicService service,
        IHubContext<EpicHub> hubContext,
        CancellationToken cancellationToken)
    {
        await service.DeleteEpic(id, cancellationToken);
        await hubContext.Clients.All.SendAsync("EpicDeleted", id, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> SubmitAgreement(
        string id,
        SubmitAgreementRequest body,
        EpicService service,
        IHubContext<EpicHub> hubContext,
        CancellationToken cancellationToken)
    {
        await service.SubmitAgreement(id, body, cancellationToken);
    var result = await service.GetEpic(id, cancellationToken);
        await hubContext.Clients.All.SendAsync("EpicUpdated", result, cancellationToken);
        return Results.Ok(result);
    }
}
