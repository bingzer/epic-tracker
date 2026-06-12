# Epic Governance

## Directory Structure

All epic files live under this epic's folder. Use this layout:

```
epics/<slug>/
  epic.md          — epic document (written during drafting)
  governance.md    — governance rules (this file)
  specs/           — one spec file per concern
  mockups/         — mockups and design artifacts
  output/          — deliverables and generated artifacts
```

## Epic Document Format

The epic document (`epic.md`) is written during drafting and updated throughout the process. It must follow this structure:

```markdown
# Epic: <title>

## Goal
One sentence describing what this epic delivers.

## Background
Why this is being done. Context, motivation, or problem being solved.

## Scope
- What is included.

## Out of Scope
- What is explicitly excluded.

## Open Questions
All open questions must be resolved during waterproofing before the epic can advance to spec_writing.
- [ ] <question — can be added at any stage by any agent or human; tick off when resolved>  

## Waterproofing

### Iteration 1
| Agent | Vote | Key Insight |
|-------|------|-------------|
| <agent> | AGREE / DISAGREE / BLOCKED | <insight> |
```

## Agent Roles

| Role           | Has MCP Tools? | Responsibility                  |
|----------------|----------------|---------------------------------|
| Epic Agent     | Yes            | Drive state machine, coordinate |
| Coding Agent   | No             | Implement specs, write docs     |
| Reviewer Agent | No             | Review coding agent output      |

---

## Epic Agent

The epic agent drives the state machine, orchestrates agents, and surfaces decisions to humans. It does not write code, specs, or documents — it delegates everything to coding agents via tmux and tracks their work.

Coding agents cannot call `advance`, `update_spec`, or any Epic Tracker tool. The epic agent must:

- Give them full context in the tmux message: spec ID, spec doc path, governance path, acceptance criteria, what done looks like.
- Call `advance` and `update_spec` on their behalf after they signal completion.

### Tmux Broker

All agent communication goes through tmux broker MCP tools:

- `start_agent` — Spawn a new Claude Code instance in a named session.
- `send_message` — Send an assignment to an existing agent session.
- `get_message` — Poll for a reply from an agent session.

### Agent Swarm

A consensus round where coding agents discuss peer-to-peer via a broker channel and submit their assessments before the epic proceeds. Only raise when `EpicAgentInstruction` tells you to.

The state machine handles channel creation, member invites, and kickoff message posting automatically. Your role as coordinator is:

1. Call `raise_agent_swarm(objective, toStateName)`, then `advance` — channel, invites, and kickoff message are all handled automatically by the server.
2. Step back and go idle. Do NOT call `advance`, poll, or message agents again. Wait until ALL participants have posted `VOTE: AGREE | DISAGREE | BLOCKED`.
3. When all participants have voted:
   - Update the epic document to record each agent's conclusion and key insights
   - Call `submit_agreement` for each agent on their behalf (coding agents cannot call it themselves)
   - Call `advance`
4. If an agent does not respond, submit a disagreement with a note that they were unreachable.
5. Disagreement triggers another iteration (max 5, then `human_in_loop` fires automatically).

### Code Review

The epic agent owns the reviewer handoff. When a spec reaches `code_review`:

1. Send the reviewer their assignment via tmux-broker (spec doc, output directory, AC to review against, required signal format).
2. Wait for their verdict. They report directly to the epic agent — not to the coding agent.
3. On approval → spec advances to `ac` (or `done` if AC not required).
4. On rejection → spec routes back to `coding`. The iteration counter increments.
5. After 5 rejections → `spec_human_in_loop` is raised. Human decides whether to override to AC or send back to coding.

### Scope Changes

A coding agent may discover mid-implementation that the work is larger than the spec describes. When this happens:

1. The coding agent signals: `SPEC {specId} SCOPE CHANGE: <description>`
2. Call `flag_scope_change(specId, description)` — this blocks the spec from advancing.
3. Raise `human_in_loop` on the epic with the scope change details for human approval.
4. tmux wakes you when the human decides.
5. Call `update_spec(specId, ScopeChangeApproved, true/false)` — this automatically unblocks the spec.
   - Approved → coding agent updates the spec doc to reflect the new scope, then continues.
   - Rejected → coding agent sticks to the original scope.

Do not let a coding agent silently expand scope. If they signal a scope change, stop and flag it before they continue.

### Human in Loop

Pauses the epic for a human decision via the dashboard. Only raise when `EpicAgentInstruction` tells you to.

1. Call `raise_human_in_loop(questions, approveToStateName, rejectToStateName)`.
2. Immediately call `advance` — epic blocks until the human responds.
3. tmux wakes you when the human decides.
4. Call `get_epic` to read the decision, then `advance` to route forward.

### Deliverables

When all specs are done, compile `output/deliverable.md` before raising human-in-loop. Message each coding agent via tmux to provide their summary, then stitch them together.

Required format:

```markdown
# Deliverable: <epic name>

## Summary
One paragraph describing what was built overall.

## Specs

### <spec-id> — <spec name>
**Agent:** <agent session name>
**What changed:** Describe what was built or modified.
**Files:** List absolute paths of created or modified files.
**How to verify:** What should the reviewer run, open, or check to confirm the work is correct.
```

Do not write this file yourself — collect summaries from the coding agents who did the work.

---

## Coding Agent

Coding agents implement specs and write documents. They do not have access to Epic Tracker MCP tools — the epic agent calls `advance` and `update_spec` on their behalf.

### Spec Format

Each spec document must follow this structure:

```markdown
# Spec: <name>

## Assigned Agent
<agent session name>

## Goal
One sentence describing what this spec delivers.

## Scope
- Bullet list of what is included.

## Out of Scope
- Bullet list of what is explicitly excluded.

## Acceptance Criteria
- [ ] Testable, observable condition 1
- [ ] Testable, observable condition 2

## Development Plan
- [ ] Implementation step 1
- [ ] Implementation step 2

## Deliverables
- [ ] /absolute/path/to/deliverable.md

## Files Affected
- Absolute paths to files that will be created or modified.
```

Save spec files under `specs/`, mockups under `mockups/`, and other output under `output/`. Always use absolute paths when reporting file locations back to the epic agent.

### Signaling Completion

When done with a spec, reply to the epic agent with:

```
SPEC {specId} STATUS: done
```

To signal a scope change:

```
SPEC {specId} SCOPE CHANGE: <description>
```

Do not begin coding until the epic agent explicitly assigns a spec. Do not call any Epic Tracker MCP tools.

---

## Reviewer Agent

Reviewer agents assess coding agent output against the spec's acceptance criteria. They report directly to the epic agent — not to the coding agent.

When assigned a review, the epic agent will instruct you to message the coding agent first and ask which files changed, any reference docs consulted, and any extra conventions applied — so you can read those directly rather than scanning the whole codebase.

Use this signal format when replying:

```
SPEC {specId} STATUS: reviewing
SPEC {specId} STATUS: review-approved
SPEC {specId} STATUS: review-rejected REASON: <reason>
```

---

## Epic States

| State          | Blocks until                  | Then routes to         |
|----------------|-------------------------------|------------------------|
| drafting       | IsDocDrafted = true           | waterproofing          |
| waterproofing  | Agent swarm consensus         | mockup or spec_writing |
| mockup         | IsMockupDone + human approval | waterproofing          |
| spec_writing   | Swarm consensus + human ok    | implementation         |
| implementation | All specs done + human ok     | closed                 |
| agent_swarm    | All agents vote               | toStateName            |
| human_in_loop  | Human decision                | approve/rejectToState  |
| closed         | Terminal                      |                        |

## Spec States

| State               | Blocks until                         | Then routes to                                      |
|---------------------|--------------------------------------|-----------------------------------------------------|
| spec_drafting       | IsSpecApproved = true + file on disk | ready                                               |
| ready               | Human clicks "Code Now" AND all dependencies in `ac` or `done` | coding                       |
| coding              | IsCodeDone = true                    | code_review or ac                                   |
| code_review         | IsCodeReviewApproved set             | ac (approved) / coding (rejected, up to 5x) / spec_human_in_loop (after 5 rejections) |
| ac                  | IsAcPassed set                       | spec_human_in_loop (always — for human sign-off)    |
| spec_human_in_loop  | Human approves or rejects            | approveToStateName / rejectToStateName              |
| done                | Terminal                             |                                                     |

### Spec Dependencies

A spec can depend on one or more other specs. A dependent spec stays blocked at `ready` until all dependencies reach `ac` or `done` — the "Code Now" button is disabled until then.

Set via `create_spec` (`dependsOn`: comma-separated spec IDs) or update later with `update_spec(specId, DependsOn, "spec-a,spec-b")`. Pass an empty string to clear all dependencies.
