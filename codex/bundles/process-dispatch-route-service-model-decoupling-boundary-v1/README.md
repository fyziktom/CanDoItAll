# process-dispatch-route-service-model-decoupling-boundary-v1

Prepared: 2026-06-06

## Purpose

Continue the `maf-processes-refactor` route-dispatch refactor after `process-dispatch-route-handler-facet-implementation-boundary-v1`.

The previous bundle successfully moved route handlers to top-level module-local classes and introduced explicit route facets. The next unsafe coupling is now the route service/model boundary:

- `ProcessDispatchRouteFacets.cs` still aliases dispatcher-owned nested types such as `DispatchCandidate`, `DispatchExecutionOutcome`, and `ProcessStepDispatchClaim`.
- `ProcessDispatchRouteServices.cs` is still one broad all-facet dispatcher-backed adapter that implements all route facets and forwards most calls back to `ProcessRunAutomationDispatchService`.
- `ProcessDispatchRouteHandlerFactory.cs` still passes one `ProcessDispatchRouteServices` instance into every handler.
- `ProcessDispatchRouteContext` and execution-route flow still carry dispatcher-owned execution/candidate models.
- The pipeline is cleaner, but it is not yet ready for `CanDoItAll.Processes.Core`.

## Non-negotiable constraints

- Do **not** create `CanDoItAll.Processes.Core`.
- Do **not** create production driver APIs, driver packs, driver registries, or `IProcessDriverPack`.
- Do **not** remove, skip, simplify, or weaken any existing process functionality.
- Do **not** change route stage order.
- Do **not** touch UI, Razor, CSS, JS, TS, screenshots, small/medium/mobile proof, or responsive optimization.
- Keep all work module-local under `CanDoItAll.Modules.Processes`.
- Preserve existing dispatch behavior for process automation, workflow route, subprocess route, direct-agent route, artifact recovery, materialization, claim lifecycle, and finalizer transitions.

## High-level target

Introduce module-local route read models and narrow route service implementations so route handlers and route facets no longer depend directly on dispatcher nested model aliases or one all-facet dispatcher service.

This is a preparation step for future Process Core and future driver packs, not the Process Core split itself.
