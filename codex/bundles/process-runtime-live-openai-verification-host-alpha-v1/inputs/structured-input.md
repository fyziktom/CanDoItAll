# Structured Input

## Primary objective
Move from restored deterministic process runtime toward a stable generic Process Core with domain drivers by adding a guarded live OpenAI process proof and a narrow verification-only process driver runtime host alpha.

## Current verified baseline
- Application startup and `/health` are source/test-backed.
- Process templates are visible via API and UI launch surfaces.
- `/processes`, project-scoped processes, and project-structure start paths are large-desktop/API proven.
- Persisted run lifecycle, durable outbox/claim, route/finalizer/artifact readback, MAF workflow-backed role, direct-agent fake-provider route, `.NET` deterministic scenario, business-analysis deterministic scenario, scheduler-origin and workflow-origin starts, run detail/recovery UI, and operator readbacks are release-candidate tested.
- Full unit and focused integration matrices pass in the previous bundle proof.
- Live OpenAI proof was skipped by policy; it is not yet a provider functionality pass.

## Next outcome
A user-facing process runtime remains stable, and a first generic verification-only driver runtime host alpha exists with explicit registry/selector/DI/manager-readonly integration. The host must not execute commands, call Graph/Office, write workspace/storage, mutate process state, apply transitions/finalizers, claim dispatch, or schedule retries.

## Explicit non-goals
- No execution-capable process drivers.
- No shell/package restore through drivers.
- No automatic driver fallback selector.
- No scheduler/workflow driver execution hook.
- No process Core dependency on driver packages or process module.
- No small/medium/mobile browser proof.
