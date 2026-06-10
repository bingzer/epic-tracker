using System.ComponentModel;
using EpicTracker.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using ModelContextProtocol.Server;

namespace EpicTracker.Api.Mcp;

public record EpicSummary(string Id, string Name, string Slug, string CurrentStateName, string EpicAgentName);
public record AdvanceEpicResult(string Id, string CurrentStateName, string? EpicAgentInstruction, HumanInLoop? HumanInLoop);
public record AdvanceSpecResult(string Id, string CurrentStateName, string? EpicAgentInstruction);
public record CreateSpecResult(string Id, string CurrentStateName);
public record UpdateSpecResult(bool Ok);

[McpServerToolType]
public class EpicAgentMcpTools(EpicService service, IHubContext<EpicHub> hubContext)
{
    [McpServerTool(Name = "list_epics"), Description("Returns slim summaries of all epics. Use this to discover active epics or find the epicId you need. Call get_epic for full detail.")]
    public async Task<List<EpicSummary>> ListEpics(CancellationToken cancellationToken = default)
    {
        var epics = await service.ListEpics(cancellationToken);
        return epics.Select(e => new EpicSummary(e.Id, e.Name ?? string.Empty, e.Slug ?? string.Empty, e.CurrentStateName, e.EpicAgentName)).ToList();
    }

    [McpServerTool(Name = "get_epic"), Description("Gets the current state of an epic, including its state name, agent instruction, typed flag fields, and all specs.")]
    public async Task<Epic> GetEpic(
        [Description("The ID of the epic to retrieve.")] string epicId,
        CancellationToken cancellationToken = default)
    {
        var epic = await service.GetEpic(epicId, cancellationToken);

        var directive = EpicAgentDirective.Build(epic.EpicGovernancePath);

        epic.SetEpicAgentInstruction(directive + "\n\n" + epic.EpicAgentInstruction);

        return epic;
    }

    [McpServerTool(Name = "get_epic_history"), Description("Returns the full audit log for an epic in chronological order. Each row records an activity: action type, epic/spec state, actor, and a JSON message blob with event-specific details.")]
    public Task<List<AuditLog>> GetEpicHistory(
        [Description("The ID of the epic.")] string epicId,
        CancellationToken cancellationToken = default)
        => service.GetEpicHistory(epicId, cancellationToken);

    [McpServerTool(Name = "advance"), Description("Advances the epic state machine one step. Call this after completing the current EpicAgentInstruction. Returns the same state (with a new instruction) when blocked waiting for external input — keep calling after acting on each instruction. Throws if epicAgentId does not match the epic's assigned agent.")]
    public async Task<AdvanceEpicResult> Advance(
        [Description("The ID of the epic to advance.")] string epicId,
        [Description("The ID of the epic agent making the call.")] string epicAgentId,
        CancellationToken cancellationToken = default)
    {
        var result = await service.Advance(epicId, new AdvanceEpicRequest(epicAgentId), cancellationToken);
        await hubContext.Clients.All.SendAsync("EpicUpdated", result, cancellationToken);
        return new AdvanceEpicResult(result.Id, result.CurrentStateName, result.EpicAgentInstruction, result.HumanInLoop);
    }

    [McpServerTool(Name = "raise_agent_swarm"), Description("Raises an agent swarm event on the epic, signalling that a group of coding agents should be spawned to work toward an objective. Call advance after this to transition into the swarm-waiting state.")]
    public async Task RaiseAgentSwarm(
        [Description("The ID of the epic.")] string epicId,
        [Description("The objective the agent swarm should achieve.")] string objective,
        [Description("The state name the epic should transition to once the swarm completes.")] string toStateName,
        CancellationToken cancellationToken = default)
    {
        await service.RaiseAgentSwarm(epicId, new RaiseAgentSwarmRequest(objective, toStateName), cancellationToken);
        var result = await service.GetEpic(epicId, cancellationToken);
        await hubContext.Clients.All.SendAsync("EpicUpdated", result, cancellationToken);
    }

    [McpServerTool(Name = "submit_agreement"), Description("Submits an agreement vote from an agent. Used during consensus states where all agents must agree before advancing. DISAGREE means the agent has domain input or the epic does not yet reflect their knowledge. AGREE means the agent has reviewed the updated epic and has nothing more to add (LGTM). Precondition: epic.AgentSwarm must not be null — throws if no swarm is active. Throws if agentId is not a member of the current swarm.")]
    public async Task SubmitAgreement(
        [Description("The ID of the epic.")] string epicId,
        [Description("The ID of the agent submitting the agreement.")] string agentId,
        [Description("Whether the agent agrees.")] bool hasAgreed,
        [Description("Optional note explaining the agent's position.")] string? note = null,
        CancellationToken cancellationToken = default)
    {
        await service.SubmitAgreement(epicId, new SubmitAgreementRequest(agentId, hasAgreed, note), cancellationToken);
        var result = await service.GetEpic(epicId, cancellationToken);
        await hubContext.Clients.All.SendAsync("EpicUpdated", result, cancellationToken);
    }

    [McpServerTool(Name = "raise_human_in_loop"), Description("Raises a human-in-loop event on the epic. Can be called at ANY point in the process whenever human judgment is needed to resolve a conflict or ambiguity — not just at designated gates. You must call advance immediately after — advance will transition the epic to human_in_loop state where it will block until the human responds via the dashboard. The epic will then route to approveToStateName or rejectToStateName.")]
    public async Task RaiseHumanInLoop(
        [Description("The ID of the epic.")] string epicId,
        [Description("The questions or context to present to the human reviewer.")] string questions,
        [Description("The state name to transition to if the human approves.")] string approveToStateName,
        [Description("The state name to transition to if the human rejects.")] string rejectToStateName,
        CancellationToken cancellationToken = default)
    {
        await service.RaiseHumanInLoop(epicId, new RaiseHumanInLoopRequest(questions, approveToStateName, rejectToStateName), cancellationToken);
        var result = await service.GetEpic(epicId, cancellationToken);
        await hubContext.Clients.All.SendAsync("EpicUpdated", result, cancellationToken);
    }

    [McpServerTool(Name = "create_spec"), Description("Creates a new spec (unit of work) under the given epic and assigns it to a coding agent.")]
    public async Task<CreateSpecResult> CreateSpec(
        [Description("The ID of the epic this spec belongs to.")] string epicId,
        [Description("A short human-readable name for this spec (e.g. 'auth-flow', 'user-profile'). Used to generate the spec ID.")] string specName,
        [Description("The name of the coding agent assigned to implement this spec.")] string assignedAgentName,
        [Description("Optional absolute path to the spec document. Must be an absolute path (e.g. C:\\Users\\... or /home/...) — relative paths will be rejected.")] string? specDocPath = null,
        [Description("Whether a code review is required before the spec can be closed. Null means inherit from epic.")] bool? isCodeReviewRequired = null,
        [Description("The ID of the agent who will review the code, if code review is required.")] string? reviewerAgentId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await service.CreateSpec(epicId, new CreateSpecRequest(specName, assignedAgentName, specDocPath, isCodeReviewRequired, reviewerAgentId), cancellationToken);
        var epic = await service.GetEpic(epicId, cancellationToken);
        await hubContext.Clients.All.SendAsync("EpicUpdated", epic, cancellationToken);
        return new CreateSpecResult(result.Id, result.CurrentStateName);
    }

    [McpServerTool(Name = "get_spec"), Description("Gets the current state of a spec, including its state name and assigned agent.")]
    public Task<Spec> GetSpec(
        [Description("The ID of the spec to retrieve.")] string specId,
        CancellationToken cancellationToken = default)
        => service.GetSpec(specId, cancellationToken);

    [McpServerTool(Name = "advance_spec"), Description("Advances the spec state machine one step. Call this (as the epic agent) after acting on the current EpicAgentInstruction for a spec. Coding agents cannot call this — only the epic agent can.")]
    public async Task<AdvanceSpecResult> AdvanceSpec(
        [Description("The ID of the spec to advance.")] string specId,
        CancellationToken cancellationToken = default)
    {
        var result = await service.AdvanceSpec(specId, cancellationToken);
        await hubContext.Clients.All.SendAsync("SpecUpdated", result, cancellationToken);
        return new AdvanceSpecResult(result.Id, result.CurrentStateName, result.EpicAgentInstruction);
    }

    [McpServerTool(Name = "update_epic"), Description("Sets a single field on an epic. Available fields: Name (string), Brief (string), EpicAgentName (string), CodingAgentNames (comma-separated string), NeedsMockup (bool), IsBriefRefined (bool), IsDocDrafted (bool), IsMockupDone (bool).")]
    public async Task<Epic> UpdateEpic(
        [Description("The ID of the epic to update.")] string epicId,
        [Description("The name of the field to set.")] string fieldName,
        [Description("The string value to set. Booleans should be 'true' or 'false'.")] string value,
        CancellationToken cancellationToken = default)
    {
        var result = await service.UpdateEpicField(epicId, fieldName, value, cancellationToken);
        await hubContext.Clients.All.SendAsync("EpicUpdated", result, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "flag_scope_change"), Description("Flags a scope change on a spec. Blocks the spec from advancing until a human approves or rejects the scope expansion via raise_human_in_loop on the epic.")]
    public async Task<Spec> FlagScopeChange(
        [Description("The ID of the spec.")] string specId,
        [Description("Description of the scope change discovered.")] string description,
        CancellationToken cancellationToken = default)
    {
        var result = await service.FlagScopeChange(specId, new FlagScopeChangeRequest(description), cancellationToken);
        await hubContext.Clients.All.SendAsync("SpecUpdated", result, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "update_spec"), Description("Sets a single field on a spec. Available fields: AssignedAgentName (string), ReviewerAgentName (string), SpecDocPath (string), IsACRequired (bool), IsCodeReviewRequired (bool), IsCodeDone (bool), IsAcPassed (bool), IsCodeReviewApproved (bool), ScopeChangeApproved (bool).")]
    public async Task<UpdateSpecResult> UpdateSpec(
        [Description("The ID of the spec to update.")] string specId,
        [Description("The name of the field to set.")] string fieldName,
        [Description("The string value to set. Booleans should be 'true' or 'false'.")] string value,
        CancellationToken cancellationToken = default)
    {
        var result = await service.UpdateSpecField(specId, fieldName, value, cancellationToken);
        await hubContext.Clients.All.SendAsync("SpecUpdated", result, cancellationToken);
        return new UpdateSpecResult(true);
    }

}
