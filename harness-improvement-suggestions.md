# Epic Tracker Harness — Improvement Suggestions

**Epic:** willow-myterritory  
**Logged by:** PM agent  
**Date:** 2026-06-09  
**Context:** Observations from driving the `willow-myterritory-sqlgen-territory-capability` spec through its state machine lifecycle.

---

## Suggestion 1 — Fix reviewer name rendering in `advance_spec` code_review instruction

### What state was affected
`code_review` — the state entered after `update_spec(IsCodeDone, true)` auto-advances the spec.

### What happened
The instruction returned by `advance_spec` when entering `code_review` rendered as:

> *"knowledgetree has sent their deliverables directly to .*  
> *Wait for messages in this format: SPEC ... STATUS: reviewing …"*

The reviewer name variable was blank — the instruction read "sent their deliverables directly to **.**" with no name. As a result, PM did not know:
- Who the reviewer was
- Whether knowledgetree had already contacted them
- Whether PM needed to broker the handoff

This caused PM to act as a relay between knowledgetree and seniordev (fetching file paths from knowledgetree, forwarding them to seniordev), which is explicitly prohibited by the governance doc.

### Root cause
Template variable for reviewer agent name was not populated in the `code_review` instruction text.

### Suggested fix
The `code_review` instruction must spell out the full three-step protocol explicitly:

> **Step 1 — PM → knowledgetree:**  
> *"Go to seniordev directly. Send them the spec doc and all files they need to review. Do not route through PM."*
>
> **Step 2 — seniordev does the review**
>
> **Step 3 — seniordev → PM AND knowledgetree:**  
> *"Send your verdict (approved / rejected + reason) to both PM and knowledgetree, so PM can update the harness and knowledgetree knows the outcome."*

The current instruction only tells PM to wait for seniordev's status messages. It says nothing about:
- PM telling knowledgetree to go to seniordev directly with the files
- seniordev copying knowledgetree on the verdict

Without this, PM becomes an accidental relay and knowledgetree is left in the dark on the review outcome.

Also consider: add a new MCP tool `get_spec(specId)` that returns the full spec record including `reviewerAgentName`, so PM can look it up independently if the instruction is incomplete.

---

## Suggestion 2 — Add a formal "scope expansion" state or MCP tool

### What state was affected
`code_review` — scope expansion was discovered after the spec had already advanced past `coding`.

### What happened
Knowledgetree completed implementation and the spec moved to `code_review`. During implementation, Knowledgetree discovered that the spec's "out of scope" assumption (*"no chatbot changes needed"*) was falsified by testing — two Willow chatbot prompt changes were required for the feature to work end-to-end.

At the point this was flagged, the spec was in `code_review`. There was no state machine path to handle this. PM had to:
1. Handle the scope decision out-of-band with Rickster via direct conversation
2. Manually update the spec doc
3. Tell knowledgetree to apply the additional changes
4. Notify seniordev that the scope had changed and they needed to review additional files

None of this was guided by the state machine. It was ad-hoc coordination.

### Suggested fix — Option A: New `scope_change` state
Add a `scope_change` state that can be entered from `coding` or `code_review`. The state:
- Pauses the spec
- Routes to `raise_human_in_loop` for scope approval
- On approval: updates the spec doc and routes back to `coding`
- On rejection: routes back to previous state unchanged

Trigger: coding agent sends a structured scope flag message, PM calls a new `flag_scope_change(specId, description)` MCP tool.

### Suggested fix — Option B: New `flag_scope_change` MCP tool only
Without a new state, add `flag_scope_change(specId, description)` that:
- Records the scope flag on the spec
- Returns a `human_in_loop` ticket ID
- Blocks `advance_spec` until the flag is resolved (approved or rejected)

This keeps the state machine simple while formalizing the process.

---

## Suggestion 3 — Distinguish "implementation smoke test" from formal AC in coding agent protocol

### What state was affected
`coding` and the transition to `code_review`.

### What happened
Knowledgetree ran all three AC scenarios during implementation and reported them as passing before the spec entered the formal `ac` state. The results were accurate and useful, but they existed in an ambiguous status — they were neither formally recorded nor clearly labeled as pre-AC verification.

This created confusion: when knowledgetree sent `coding-done`, they included detailed AC pass results. PM had to decide whether to record these or wait for the formal `ac` state after code review.

### Suggested fix
Two options:

**Option A — Convention only:** Instruct coding agents (via the `coding` state instruction) to label pre-AC testing as "implementation smoke test" not "AC results." PM notes them but does not record `IsAcPassed` until the `ac` state.

**Option B — New MCP tool `record_smoke_test(specId, results)`:** Allows coding agents to formally log their pre-AC test results against the spec without setting `IsAcPassed`. PM can surface these in the `ac` state instruction so the reviewer/AC agent has context.

---

## Suggestion 4 — Validate spec output paths against epic slug at spec creation

### What state was affected
`spec_writing` / spec doc authoring (upstream of the implementation we ran).

### What happened
The QA guide spec (`willow-territory-qa-guide`) had an incorrect output path in its Acceptance Criteria and Files Affected sections:

```
O:\agents\pm\epics\willow-my-territory\output\qa-guide.md
```

The correct epic slug is `willow-myterritory` (no hyphen before "myterritory"), so the correct path is:

```
O:\agents\pm\epics\willow-myterritory\output\qa-guide.html
```

This was caught and corrected manually. If knowledgetree had written the file to the wrong path, the deliverable would have landed outside the epic directory and been invisible to the state machine.

### Suggested fix
When `create_spec` or `update_spec(SpecDocPath)` is called, validate that any file paths in the spec doc that begin with the `basePath` contain the correct epic slug. Return a warning or error if the path contains a variant slug (e.g. `willow-my-territory` vs `willow-myterritory`).

Alternatively: expose `outputDirectory` in the `get_epic` response (it already exists in the epic record) and include it explicitly in the `coding` state instruction so coding agents have the canonical path handed to them.

---

## Suggestion 5 — Add `get_spec(specId)` MCP tool

### What was missing
Throughout the implementation flow, PM had to call `get_epic` to inspect spec state, wading through the full epic payload to find one spec's fields. There is no direct tool to fetch a single spec record.

### Suggested fix
Add `get_spec(specId)` returning the full spec record: `currentStateName`, `assignedAgentName`, `reviewerAgentName`, `specDocPath`, all boolean flags, and the current `specAgentInstruction`. This would:
- Let PM verify spec state without polluting context with the full epic
- Allow PM to recover reviewer name if the `advance_spec` instruction omits it (see Suggestion 1)
- Make the PM agent more resilient when instructions are incomplete

---

## Suggestion 6 — Include governance path in every `advance_spec` instruction

### What was missing
The `advance_spec` instructions reference the spec doc path but do not repeat the governance path. PM has to either remember it or re-read `get_epic` to find it.

### Suggested fix
Include `governancePath` in every `advance_spec` instruction returned, e.g.:

> *"Governance: O:\agents\pm\epics\willow-myterritory\governance.md"*

This is low-cost and ensures PM always has the reference when composing agent messages.

---

## Suggestion 7 — Enforce AC checkboxes and Development Plan section in spec format

### What was missing
Spec documents reviewed during `willow-myterritory` used plain bullet lists for Acceptance Criteria and had no Development Plan section. This means:
- AC items cannot be checked off as they are verified — pass/fail is a judgment call rather than a tracked checklist
- There is no structured implementation plan inside the spec, making it hard to verify completeness at the `done` gate (governance rule: "No epic is done until you have verified every checkbox")

### Current behavior
The spec template (in per-epic `governance.md`) defines Acceptance Criteria as a plain bullet list. There is no Development Plan section in the template at all.

### Suggested fix — Option A: Enforce in spec template
Update the harness-generated spec template to require:

```markdown
## Acceptance Criteria
- [ ] Testable, observable condition 1
- [ ] Testable, observable condition 2

## Development Plan
- [ ] Implementation step 1
- [ ] Implementation step 2
```

Coding agents fill in the checkboxes; PM and the reviewer can verify each is ticked before advancing.

### Suggested fix — Option B: Validate on `update_spec(IsCodeDone, true)`
Before accepting `IsCodeDone = true`, the harness reads the spec doc and checks:
- All `- [ ]` items under `## Acceptance Criteria` are checked (`- [x]`)
- All `- [ ]` items under `## Development Plan` are checked (`- [x]`)

If any are unchecked, `update_spec` returns an error and the spec does not advance.

### Why this matters
The governance doc already states: *"Never mark a spec or epic done if any `- [ ]` item is unchecked."* But that rule can only be enforced if the spec actually uses checkboxes. The harness should make checkboxes the default, not an optional convention.
