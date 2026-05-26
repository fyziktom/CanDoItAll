# Current State

Phase8 improved many things enough that a real UI-driven process test is now plausible, but not yet safe without one more readiness pass.

## Improvements verified from reviewed source

- `ProcessStepRecoveryOption.None` exists.
- Project-structure tools are explicitly listed in `AgentToolInvocationPolicyMetadata`.
- Project-structure mutation tools are classified as mutation and are mapped to `ExecuteExternalAction`.
- Blazor revalidation and writeback steps no longer use product mutation in the reviewed `blazor-app-delivery` template.
- The process API skill now documents key governance fields.
- A seeded Blazor WASM PWA baseline scenario exists in `baseline-scenarios.json`.

## Gaps

- The existing seeded scenario is a regression fixture with transitions and artifacts. That is not the same as a live agent-executed process profile.
- We still need a role/agent/tool/skill matrix proving each role has exactly the tools it needs and no unsafe extras.
- We need an explicit live UI runbook and Playwright preflight harness.
- Non-software templates must remain protected so the core does not become software-only.
