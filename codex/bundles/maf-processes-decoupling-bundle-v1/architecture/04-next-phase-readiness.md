# Next Phase Readiness

## Current Closure State

This bundle completed the first dependency-inversion phase. `CanDoItAll.AgentFramework.Maf` now depends on `CanDoItAll.AgentFramework.Tooling` and composes registered `IAgentRuntimeToolProvider` instances. The Processes module owns `ProcessAgentRuntimeToolProvider` and registers it when the Processes module is loaded.

The closure proof is intentionally scoped:

- Direct MAF project/source/docs references to `CanDoItAll.Modules.Processes`, `ProcessToolBuilder`, `CreateProcessToolBuilder`, and `MafAgentRuntime.ProcessTools` are absent.
- All 23 process tools remain exact-name tested through provider/runtime, policy catalog, and capability registry coverage.
- Real app composition registers the Processes provider and MAF starts without process providers.
- Process outbox, tool-receipt, and current-run artifact-lineage smoke tests still pass.
- Documentation describes the provider seam and does not claim that process-core extraction or driver packs are complete.

This bundle does not claim the full transitive solution graph is process-free. Workbench/process integration and broader process runtime ownership remain in scope for later architecture work.

## Recommended Next Bundle

```text
Process contracts/core extraction foundation
```

Do not start that work inside this bundle.

Recommended next-phase order:

1. Extract `CanDoItAll.Processes.Contracts` for entity-free process request/result DTOs used by tools, APIs, scheduler, and integration layers.
2. Extract small pure process-core policies: transition guard, run status resolver, definition linter, artifact status projection where dependency-safe.
3. Introduce a process agent execution gateway to reduce direct Processes -> AgentFramework implementation dependency.
4. Only after that introduce `IProcessDriverPack` and domain driver packs.

## Guardrails For The Next Bundle

- Do not move dispatcher code before contracts and pure policies are separated.
- Do not introduce domain driver packs while process runtime DTOs still depend on EF entities or module-local services.
- Do not weaken process tool approval/access behavior while extracting contracts.
- Keep the 23 process tool inventory exact unless a separate feature explicitly changes the tool surface.
- Reuse the SB09 test set as the entry smoke for the next bundle: hidden MAF dependency scan, provider/policy tests, provider composition integration, process outbox, receipt semantics, artifact lineage, and full build.
