# Current state analysis

## What is done

The previous route service/model decoupling bundle reports completion. The key proof states that route-facing dispatcher nested model references were removed from route-facing files and that route services now consume route-owned models via explicit adapters.

Observed current state from branch `maf-processes-refactor`:

- `ProcessDispatchRouteModels.cs` defines route-owned `ProcessRouteCandidate`, `ProcessRouteDispatchClaim`, and `ProcessRouteExecutionOutcome` models.
- `ProcessDispatchRouteModelAdapters.cs` is the explicit bridge to dispatcher-owned nested models.
- `ProcessDispatchRouteServices.cs` is split into route-specific service classes, but most of them still forward to `ProcessRunAutomationDispatchService` through the adapter.
- `ProcessDispatchRouteHandlers.cs` contains top-level route handlers and uses route facets instead of taking `ProcessRunAutomationDispatchService` directly.
- `ProcessRunAutomationDispatchService.RouteExecution.cs` has become a small orchestration shell around heartbeat/claim handling and route pipeline execution.

## Why Process Core is still not recommended as the next step

The system is significantly cleaner, but Process Core extraction would still be premature because several adapters still depend on the dispatcher as a service locator/orchestration hub. Key risks:

- Route service classes still forward to dispatcher methods.
- Candidate hydration and direct-agent binding still use dispatcher-owned models and many module services together.
- Subprocess runtime/projection logic still mixes DB reads, transition writes, artifact projection decisions, and finalizer calls.
- Finalizer and transition application still need a clearer module-local boundary.
- The dispatcher still owns too many static helper entry points used by projection, route, and validation helpers.

The next work should reduce those couplings module-locally first. After that, a small `Processes.Core` extraction can be evaluated with a much lower risk.

## Current hotspot inventory

| Hotspot | Current problem | Direction |
| --- | --- | --- |
| `ProcessDispatchRouteServices.cs` | Narrow class names, but many methods still forward to dispatcher methods | Move real side-effect ownership into route services/coordinators where safe |
| `ProcessDispatchRouteModelAdapters.cs` | Explicit bridge is correct, but still preserves dispatcher-owned source handles | Reduce required conversions by moving downstream services onto route models |
| `LoadDispatchCandidateAsync` / candidate hydration | Still one orchestration path for EF readback, artifact inputs, assignments, agent binding, recovery query | Extract candidate hydration application service and direct-agent binding boundary |
| Subprocess dispatch/projection | Still in dispatcher partial with EF reads/writes and projection writer creation | Extract subprocess runtime service and projection store/writer boundary |
| Finalizer/transition/error closure | Better separated but still dispatcher-owned | Create transition/finalizer application service and failure closure coordinator |
| Static helper forwarding | Many module-local helpers still call dispatcher static methods | Burn down wrappers only after behavior has tests |

## Expected outcome of this bundle

At the end of this bundle, the dispatcher should mostly act as a facade that composes module-local services. It should not be ready for full Core extraction yet, but it should be close enough for a final `Process Core readiness decision` bundle.
