# Target Architecture For This Stabilization Pass

## Process Core
Pure deterministic rules/read models only. No runtime, dispatcher, EF, UI, MAF, OpenAI, provider, scheduler, workflow, template family, driver, or domain-specific process concepts.

## Process Module Runtime
Owns definitions, templates, launch plans, process runs, outbox, dispatch, finalizer, artifact projection, project/project-structure UI/API, scheduler/workflow-origin starts, and runtime-host diagnostic surfaces.

## Agent/Provider Execution
Process steps execute via:
`ProcessRun -> assignment -> AgentFramework/MAF workspace provider -> managed provider profile -> provider runtime -> finalizer -> artifact/readback`.

## Runtime Host
Still read-only / dry-run only. It may provide diagnostics and operator readback. It must not execute effectful domain actions.

## Future Process Runtime Core
Deferred. Document seams only. No extraction in this bundle.
