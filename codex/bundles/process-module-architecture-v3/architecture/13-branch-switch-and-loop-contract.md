# Branch Switch And Loop Contract

## Design Intent

Branching must be typed and auditable. The current text-token routing approach is explicitly rejected. Future implementation must not infer runtime semantics from words such as repair, rework, blocked, or escalation in a branch title.

Branches are generic control-flow contracts. Domain-specific branch families can be supplied by drivers or templates, but the runtime sees only typed branch definitions, outcomes, route targets, decisions, and loop budgets.

## Model Concepts

| Concept | Meaning |
| --- | --- |
| `BranchDefinition` | Design-time branch/switch node or step contract. |
| `BranchFamily` | Reusable set of branch inputs, outcomes, and policies from template or driver. |
| `BranchOutcome` | Typed outcome with stable key, display label, semantic category, and route target. |
| `BranchInputRequirement` | Artifact, step result, manager incident, metric, user approval, or driver facet required for decision. |
| `BranchDecisionRequest` | Runtime request to manager/strategy to choose an outcome. |
| `BranchDecisionResult` | Selected outcome, confidence, rationale reference, diagnostics, and idempotency key. |
| `RouteTarget` | Next step, previous step, subprocess boundary, end state, escalation, or wait state. |
| `LoopBudget` | Max repeats by branch, path fingerprint, incident class, and recovery family. |
| `PathFingerprint` | Stable hash of branch path, failed condition, recovery attempt class, and relevant artifact/incident evidence. |

## Branch Family Sources

- Built-in generic families from core/templates.
- Driver-provided domain branch families.
- Template-defined custom families.
- Local override copies detached from a global family.

Driver-provided families expose opaque capability/facet references. Core does not know domain names.

## Branch Decision Flow

1. Runtime reaches branch/switch step.
2. Runtime validates required branch inputs are available or produces manager incident.
3. Runtime creates `BranchDecisionRequest`.
4. Bound branch decision strategy or manager chooses outcome.
5. Manager records `BranchDecisionRecorded` event.
6. Runtime validates selected route target.
7. Runtime applies route transition and consumes loop budget when route is backward or repeating.
8. Runtime emits route-applied event.

## Route Target Model

Route targets:

- `NextStep`
- `SpecificStep`
- `PreviousStep`
- `SubprocessStart`
- `SubprocessResume`
- `WaitForArtifact`
- `WaitForUser`
- `Escalate`
- `CompleteRun`
- `FailRun`
- `CancelRun`

Backward routes require:

- explicit `IsBackwardRoute = true`,
- loop budget,
- path fingerprint policy,
- escalation target,
- manager decision event.

## Local Override Model

When a user selects a branch family and customizes it:

- store selected family ID and base content hash,
- store local override patch,
- keep outcome stable IDs unless intentionally replaced,
- write conflict records when global family changes the same JSON pointer,
- preserve branch migration diagnostics.

## UI Editor Implications

The UI branch editor must:

- show branch family source and version,
- show typed outcomes, not only text fields,
- require route target selection,
- require loop budget for backward routes,
- show required inputs,
- show manager/strategy binding,
- show local override status,
- block publish if branch contract is incomplete.

The UI can edit display labels and descriptions, but display text does not determine runtime routing.

## Migration From Old Branch Outcomes

Migration tooling must:

1. read current branch outcome keys/titles/descriptions,
2. map known old system outcomes to typed outcomes where deterministic,
3. create migration diagnostics for ambiguous text-token outcomes,
4. reject automatic migration when route semantics cannot be proven,
5. preserve old labels as display text only,
6. never create runtime token routing from free text.

## Invariants

- Every branch outcome has a stable typed outcome ID.
- Every route target is validated before runtime applies it.
- Backward routes always have loop budgets and fingerprints.
- Manager-selected outcomes always create decision events.
- Display text never determines runtime branch semantics.
- Driver branch families cannot mutate runtime state directly.

## Failure Behavior

| Failure | Required response |
| --- | --- |
| Missing required branch input | Manager incident and branch waits or follows configured missing-input route. |
| Unknown branch outcome | Runtime rejects decision and escalates to manager/operator. |
| Backward route without budget | Definition publish/build failure. |
| Loop budget exceeded | Runtime emits escalation and blocks further automatic repeats. |
| Ambiguous migration from old text outcome | Migration compatibility report requires manual resolution. |

## Test Implications

- Core tests cover branch definition validation, route target validation, backward route budget enforcement, and fingerprint stability.
- Builder tests cover family resolution, local overrides, strategy binding, and migration diagnostics.
- Runtime tests cover branch decision idempotency, route application, loop budget consumption, and escalation.
- UI tests cover typed branch editor constraints and rejection of incomplete branch contracts.
