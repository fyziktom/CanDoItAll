# Target Solution

## Goal

- Add flexible branch routing to the canonical process model with the smallest coherent change that still keeps authoring, runtime, MCP, and UI in sync.

## Chosen Modeling Direction

- Keep the current step-first canonical model instead of introducing a separate orchestration subsystem.
- Extend the step definition model with explicit branch outcome records owned by the source step.
- Extend a dependent step with an optional required source outcome reference so the same source step can route to different next steps based on the selected outcome.
- Add an explicit decision-maker role requirement on the source step so the routing responsibility is durable and typed.

## Why This Model

- It solves the user’s switch-style branching case directly.
- It keeps the canonical store in the existing process definition tables and versioning flow.
- It avoids stringly typed route keys at runtime because the selection can use persisted outcome identifiers.
- It is smaller and safer than introducing a fully general transition engine when the live repo currently supports only one incoming dependency per step.

## Supported Behavior In This Pass

- One source step can define multiple named outcomes.
- Multiple downstream steps can listen to the same outcome.
- Unconditional downstream steps can still activate regardless of the selected branch when that is intentional.
- The runtime can record which outcome was selected and skip non-selected mutually exclusive branches.

## Explicit Boundary For This Pass

- This pass does not introduce full multi-predecessor join semantics or a general boolean policy engine.
- If proof exposes a real need for joins or richer guard expressions, that becomes a documented reopen or follow-up bundle instead of being smuggled into this branch fix.

## Cross-Surface Contract

- Definition save, publish, export, import, and version cloning must preserve branch data.
- Runtime step transitions must accept a selected branch outcome when required.
- Read models and MCP run detail must expose branch metadata needed by the workspace or external callers.
- The workspace authoring form, runtime actions, and canvas must all reflect the same canonical branch contract.

## Migration Rule

- Persistence changes must be additive and version-safe across SQLite and PostgreSQL migrations.
- Existing linear definitions must remain valid without forcing immediate rewrites.
