# Epic Tracker Bug Report — Live Run Observations
Date: 2026-06-02

---

## BUG-1: UI does not live-update (SignalR not wiring through)

**Symptom:** Dashboard never reflects state changes automatically. User must manually refresh the page to see epic or spec state transitions.

**Expected:** Every state change (epic advance, spec advance, approval, etc.) should push a SignalR event to all connected clients so the dashboard updates in real time.

**Root cause:** Most mutation endpoints broadcast `EpicUpdated` via SignalR after saving. However:
- `AdvanceSpec` endpoint does NOT broadcast after advancing a spec
- `ApproveSpecHumanInLoop` endpoint does NOT broadcast
- `UpdateSpecField` endpoint does NOT broadcast
- After epic advance the event fires but the UI component may not be re-rendering correctly

**Files to fix:**
- `EpicTracker.Api/Endpoints/SpecEndpoints.cs` — add `hubContext.Clients.All.SendAsync("EpicUpdated", ...)` after every mutation (advance, approve-human-in-loop, update-field)
- `EpicTracker.UI/src/hooks/useSignalR.ts` — verify the `EpicUpdated` handler is wired and updates both the epic list and the detail page state
- The detail page (`EpicDetailPage.tsx`) may need to re-fetch or merge the incoming SignalR payload on `EpicUpdated`

---

## BUG-2: Agent is NOT operating autonomously — keeps asking user via CLI

**Symptom:** The PM agent consistently prompts the user through the CLI for permission before taking the next step. Examples observed:
- "Advanced to spec_writing. Want me to instruct them?"
- "Instructing both agents... Messages sent. Waiting..."
- "Advanced to implementation. Want me to advance further and dispatch them?"

This means the headless autonomy instruction is not being followed. The agent treats each advance as an opportunity to check with the user rather than proceeding automatically.

**Expected:** The agent should loop on `advance` continuously without ever asking the user. Human interaction is only via `raise_human_in_loop` → dashboard. The `AUTONOMOUS MODE` preamble added to `DraftingState` is not sufficient — the agent needs an explicit system-level instruction or a stronger prompt contract that persists beyond the first instruction.

**Possible fixes:**
- Add the autonomy preamble to EVERY `EpicAgentInstruction` emitted by every state, not just the drafting state. Consider a base class helper on `EpicState` that prepends it.
- Or: add it as a prefix in `EpicService.Advance` before setting the instruction on the entity — one place, always applied.
- The PM agent's CLAUDE.md or system prompt may also need to enforce this at the agent level.

---

## BUG-3: tmux wake after epic HumanInLoop approval does not work

**Symptom:** After clicking the "Approve" button on the epic human-in-loop panel in the dashboard, the PM agent does not resume. User had to go back to the PM's CLI and manually tell it to check/advance.

**Expected:** `POST /api/epics/{id}/approve-human-in-loop` should call `tmux send-keys -t <epicAgent> "" Enter` to wake the blocked agent.

**Status:** This was implemented in `EpicService.ApproveHumanInLoop` → `WakeAgent(entity.EpicAgent)`. The `WakeAgent` method uses `Process.Start("tmux", ...)`. This may be silently failing (the catch block swallows all errors).

**To investigate:**
- Remove the silent catch in `WakeAgent` and log the error
- Verify `tmux` is on the PATH for the process that runs the published DLL (a hidden background process may not have the same PATH as the shell)
- Verify the session name stored in `EpicAgent` exactly matches the tmux session name (case-sensitive)

---

## BUG-4: Approve button on spec HumanInLoop does nothing

**Symptom:** Clicking "Approve" on a spec's human-in-loop panel in the dashboard: button disappears, spec state stays on `spec_human_in_loop`, nothing happens to the agent. User had to manually go to PM's CLI to prompt it to check.

**Two missing pieces:**
1. `ApproveSpecHumanInLoop` does not call `tmux send-keys` to wake the agent (no `WakeAgent` call, unlike the epic equivalent)
2. `ApproveSpecHumanInLoop` does not broadcast a SignalR `EpicUpdated` event, so the UI doesn't reflect the approval

**Files to fix:**
- `EpicTracker/Services/EpicService.cs` — `ApproveSpecHumanInLoop` needs `WakeAgent(epicEntity.EpicAgent)` after saving (requires loading the parent epic to get the agent session name)
- `EpicTracker.Api/Endpoints/SpecEndpoints.cs` — `ApproveSpecHumanInLoop` handler needs to broadcast `EpicUpdated` via SignalR after the call

---

## BUG-5: Agent swarm panel persists in UI after swarm is complete

**Symptom:** The agent swarm panel (showing the waterproofing consensus round, iteration 0) continues to display on the epic detail page even after the swarm completes and the epic moves to later states. Persists even after page refresh — the swarm data is still on the epic object.

**Expected:** Once a swarm is complete (`isComplete: true` or `hasConsensus: true`) and the epic has advanced past that state, the swarm panel should either be hidden or shown as a collapsed historical record.

**Root cause (likely):** `WaterproofingState` clears `epic.AgentSwarm = null` before transitioning to `spec_writing`. But if another swarm is raised later (e.g. in `spec_writing` for spec consensus), and then completes, `AgentSwarm` may not be cleared before final state transitions. Or the UI simply shows the swarm panel whenever `agentSwarm` is non-null without checking if the epic is already past that phase.

**Files to fix:**
- `EpicTracker.UI/src/components/EscalationPanel.tsx` (or wherever the swarm panel renders) — only show the swarm panel if the epic's `currentStateName` is `agent_swarm` or `waterproofing`, OR if the swarm is actively incomplete
- Alternatively ensure all state transitions that exit a swarm phase clear `epic.AgentSwarm = null` before saving

---

## BUG-6: Agent swarm iteration display is off by one

**Symptom:** The first waterproofing iteration is shown as "iteration 0" in the UI. Should display as "iteration 1".

**Fix:** In the UI, display `agentSwarm.iteration + 1` wherever the iteration number is shown, or increment the counter in the backend before storing (start at 1 instead of 0).

---

## Summary Table

| # | Severity | Area | Description |
|---|---|---|---|
| 1 | High | UI/SignalR | Dashboard requires manual refresh — spec mutations don't broadcast |
| 2 | High | Agent behavior | PM agent asks user via CLI instead of operating headlessly |
| 3 | High | Backend/tmux | Epic HumanInLoop approval doesn't wake agent (tmux send-keys failing silently) |
| 4 | High | Backend/tmux | Spec HumanInLoop approval doesn't wake agent and doesn't broadcast SignalR |
| 5 | Medium | UI | Agent swarm panel persists after swarm is complete |
| 6 | Low | UI | Swarm iteration display is off by one (shows 0-indexed) |
