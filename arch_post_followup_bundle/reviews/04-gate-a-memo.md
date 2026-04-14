# Architecture gate memo

## Gate
- `Gate A`

## Reviewed subbundles
- `01-live-proof-reconciliation-and-unverified-closure-reset`
- `02-process-graph-dag-invariant-hardening`

## Decision
- `Pass`

## Gate questions and answers
1. Is graph legality now enforced at save/publish time, including self-loops and larger cycles?
   - Answer: `Yes.` Raw editor validation now runs before normalization can discard illegal edges, and persisted publish validation rejects both self-references and multi-step cycles.
2. Are the runtime and canvas paths free of silent root/topological fallbacks for invalid graphs?
   - Answer: `Yes.` `StartRunAsync` now fails instead of seeding the first step when no legal root exists, and canvas recomposition now throws when the graph cannot be topologically ordered.
3. Do the new tests prove rejection of invalid graphs and preservation of valid DAG behavior?
   - Answer: `Yes.` The fresh integration/component proof covers save, publish, runtime, and recomposition rejection paths while the existing valid branching DAG tests still pass in the same suites.

## Evidence reviewed
- Commands:
- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessCanvasSurfaceFactoryTests|FullyQualifiedName~ProcessCanvasRecompositionServiceTests|FullyQualifiedName~ProcessWorkspaceTests" --logger "trx;LogFileName=graph-components.trx" --results-directory .codex-test-results\graph-components -v:minimal`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests" --logger "trx;LogFileName=graph-integration.trx" --results-directory .codex-test-results\graph-integration -v:minimal`
- Proof artifacts:
- `C:\repositories\CanDoItAll\.codex-test-results\graph-components\graph-components.trx`
- `C:\repositories\CanDoItAll\.codex-test-results\graph-integration\graph-integration.trx`
- Important diffs:
- `ProcessesService.Persistence.cs` validates before and after normalization so save cannot silently sanitize an illegal graph.
- `ProcessesService.Support.cs` now centralizes self-loop and cycle detection for editor/published graphs.
- `ProcessesService.Runtime.cs` rejects invalid published graphs instead of inventing a root node.
- `ProcessCanvasRecompositionService.cs` now throws when topological ordering fails.
- `ProcessCanvasBranching.cs` no longer strips self-references before validation sees them.

## Remaining gaps
- `F002` through `F007` remain open and still block final bundle closure.

## Corrective action
- Corrective subbundle key:
- `none`
- Required rerun commands:
- `none`

## Reviewer notes
- Gate A can pass because the bundle's graph-legality objective now holds at the service boundary and the remaining work is downstream of that foundation instead of compensating for it.
