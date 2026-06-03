# EpicTracker State Diagrams

## Epic State Machine

```
[drafting]
    │ CodingConventionPath set → BeginWaterproofing()
    ▼
[waterproofing]
    │ WaterproofingComplete=true, RequiresMockups=false
    ├──────────────────────────────────────────────────► [spec-writing]
    │
    │ WaterproofingComplete=true, RequiresMockups=true
    └──────────────────────────────────────────────────► [human_in_the_loop]
                                                              │ HumanApprove → [spec-writing]
                                                              │ HumanReject  → [waterproofing]

[spec-writing]
    │ All specs approved → BeginImplementation()
    ▼
[coding]
    │ All specs terminal (done or abandoned)
    ▼
[human_in_the_loop]
    │ HumanApprove → [closed]
    │ HumanReject  → [coding]

[closed]  ← terminal

RollbackToWaterproofing() → from any state → [waterproofing]
    (abandons all non-terminal specs, clears waterproofing rounds)
```

---

## Spec State Machine

```
[writing]
    │ Submit()
    ▼
[human_in_the_loop]  ← awaiting owner approval of spec doc
    │ HumanApprove, deps done     → [coding]
    │ HumanApprove, deps pending  → [waiting_for_deps]
    │ HumanReject                 → [writing]

[waiting_for_deps]
    │ All depended-on specs reach [done]
    ▼
[coding]
    │ StartCodeReview()
    ▼
[human_in_the_loop]  ← awaiting code review approval
    │ HumanApprove (code spec)              → [build]
    │ HumanApprove (doc, RequiresStopGate)  → [human_in_the_loop] (stop gate)
    │ HumanApprove (doc, RequiresAc)        → [ac]
    │ HumanApprove (doc, no flags)          → [done]
    │ HumanReject                           → [coding]

[build]
    │ BuildPassed()
    │ RequiresStopGate=false, RequiresAc=false  → [done]
    │ RequiresStopGate=false, RequiresAc=true   → [ac]
    │ RequiresStopGate=true                     → [human_in_the_loop] (stop gate)
    │                                                │ HumanApprove, RequiresAc=true  → [ac]
    │                                                │ HumanApprove, RequiresAc=false → [done]
    │                                                │ HumanReject                    → [abandoned]

[ac]
    │ AcPassed()
    ▼
[done]  ← terminal

[abandoned]  ← terminal  (reachable from any non-terminal state)
```

---

## State Reference

### Epic States
| State | Description |
|---|---|
| `drafting` | Epic created, awaiting coding convention and waterproofing kick-off |
| `waterproofing` | Agents reviewing the plan for gaps and blockers |
| `spec-writing` | Agents writing spec documents for their assigned work |
| `coding` | Specs approved, agents implementing |
| `human_in_the_loop` | Paused — human must approve or reject before continuing |
| `closed` | Epic complete |

### Spec States
| State | Description |
|---|---|
| `writing` | Agent drafting the spec document |
| `human_in_the_loop` | Paused — human review required (spec approval, code review, or stop gate) |
| `waiting_for_deps` | Approved but blocked on one or more dependency specs |
| `coding` | Agent implementing |
| `build` | Build verification in progress |
| `ac` | Acceptance criteria verification in progress |
| `done` | Spec complete |
| `abandoned` | Spec cancelled — will not be implemented |
