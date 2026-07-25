# terminal-summary-assembly-and-project-node

## Status

- `Completed`

## Objective

- Finalize deterministic process-run facts idempotently from the runtime event/outbox lifecycle, enrich them asynchronously through the configured manager agent, and make terminal project-structure nodes consume the record.

## Success Criteria

- Completed, failed, and cancelled terminal events trigger record assembly once without blocking the runtime commit.
- `ManagerLoopBudgetEscalated` remains an active attention event and does not create a false terminal record; `Escalated` is reserved for a future explicit ending transition.
- Hard facts aggregate runtime, assignment, plan, child-run, execution observation, usage, cost, and tool evidence with typed completeness.
- Narrative generation is leased/retryable and transitions explicitly through Pending/Generating/Completed/Failed.
- Terminal project nodes read the record; active nodes remain live progress.

## Covered Inputs

- R01-R08, R11, R13; N003-N007, N009.

## Prerequisites

- SB02 progression gate and Architecture A1 pass.

## Exact Source References

- `C:\repositories\CanDoItAll\src\Processes\CanDoItAll.Processes.Application`
- `C:\repositories\CanDoItAll\src\Processes\CanDoItAll.Processes.Runtime\ProcessRuntimeState.cs`
- `C:\repositories\CanDoItAll\src\Processes\CanDoItAll.Processes.Projections\ProcessProjectionWorker.cs`
- `C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.Processes\Services\RuntimeIntegration`
- `C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureProcessProjectionContributor.cs`

## UI Composition Contract

- Existing project-structure node composition is retained; only its terminal data source/notes/metadata change. No new layout.

## Deliverables

- Domain-neutral evidence contract and Agent Framework adapter with summary-only/batch behavior.
- Deterministic assembler and finalization event/outbox handler.
- Explicit backfill/rebuild operation using identical assembly rules.
- Manager-agent selector, structured narrative contract/generator, claim worker, retry/error policy, and logs with masked state.
- Terminal project-node integration.
- Behavioral tests for all dispositions, incomplete evidence, idempotency, supersession, root/child rollup, narrative transitions, and project nodes.

## Architecture Impact

- Application orchestrates derived read-model creation after runtime commit.
- Modules.Processes owns provider/agent policy.
- Runtime gains no projection/provider dependency.
- Workbench consumes an application query, not canonical evidence stores, for terminal summaries.

## Dependency Impact

- SB04 depends on records being complete enough for API/graph/analytics consumers and on narrative state being stable.

## Validation Depth

- Proof tier: `Behavioral`.
- Critical lifecycle foundation.

## Implementation Steps

1. Select the committed event/outbox consumer seam and characterize duplicate delivery.
2. Build one evidence snapshot with batched/projection-shaped reads; do not hydrate unrelated detail.
3. Assemble/upsert deterministic facts and completeness before scheduling narrative work.
4. Add manager selection and structured output generation behind narrow interfaces.
5. Implement durable lease/attempt/retry/completion/failure updates.
6. Switch terminal project nodes to run-record query; make pending/failed summary state visible.
7. Add tests and run Architecture Checkpoint A2.

## Scope Exceptions

- Lossless backfill is not promised where historic evidence is absent.
- No change to runtime `IsRunTerminal`; manager-loop escalation is not treated as finalization.

## Do Not Do

- Do not call an LLM inside runtime commit, event projection transaction, API GET, or project-node read.
- Do not silently substitute canned narrative on failure.
- Do not persist prompt/log/tool-argument bodies in compact records.

## Acceptance Checklist

- [x] Three canonical terminal dispositions and manager-loop escalation exclusion are proven; the reserved escalated assembler contract remains valid.
- [x] Hard facts survive narrative failure.
- [x] Lease and atomic same-source execution creation prevent duplicate concurrent generation; retry is bounded.
- [x] Missing evidence is explicit.
- [x] Terminal project node uses the record and active node behavior remains valid.
- [x] Architecture A2 passes.

## Proof Required

- Focused finalizer/assembler/evidence/narrative/project-node tests.
- Affected project builds.
- Log/privacy inspection.
- Architecture A2 decision.

## Browser Validation Logging

- N/A: no Razor, CSS, layout, dialog, or rendered component markup changed. Project-node data/notes use service/integration tests.

## Actual Proof And Progression

- Entry and closure gates: `Pass`.
- Direct production-projector tests cover completed/failed/cancelled seeding, manager-loop escalation exclusion, reactivation supersession, and later terminal revision.
- Assembler/aggregator tests cover subtree roll-up, every evidence source, cap boundaries, event aggregates, timing, repetitions, usage/cost, and negative privacy serialization.
- Store, generator, and batch tests cover hard-facts-before-narrative, atomic same-source launch/reuse, active execution deferral without attempt consumption, bounded retry, structured-output validation, and explicit failure.
- `ProjectStructureProcessRunRecordIntegrationTests` proves terminal nodes remain available after runtime state/assignment rows are purged and expose pending/failed stage state.
- Dependent-flow proof: summary/graph/analytics API mapping and workspace/dashboard/cost record consumers pass focused tests.
- Progression decision: `Completed; SB04 may treat facts as independently durable and narrative as asynchronous enrichment.`

## Behavioral Semantic Adequacy

- Raw note owned: `N003`, `N004`, `N005`, `N006`, `N007`, and `N009`: terminal hard facts, connected structured manager output, ID-based records, reusable consumers, asynchronous/deep loading, and architecture quality.
- Shipped behavior: canonical terminal events seed records after commit; the bounded worker assembles deterministic facts before a separately leased manager narrative; reactivation supersedes the record; terminal project nodes page through the narrow read-only record seam.
- Source proof: `ProcessRuntimeProjectionProjector.cs`, `ProcessRunRecordAssembler.cs`, `ProcessRunFactsAggregator.cs`, `ProcessRunRecordBatchProcessor.cs`, `AgentFrameworkProcessRunNarrativeGenerator.cs`, and `ProjectStructureProcessRunRecordProjector.cs`.
- Test proof: `ProcessProjectionPipelineTests` covers the production projector lifecycle; `ProcessRunRecordAssemblerTests` covers subtree facts/completeness/privacy; narrative/store/batch tests cover leases, retries, deferral, source reuse, and concurrent workers; project-structure unit/integration tests cover record paging and durable terminal rendering.
- Shallow-pass trap: seeding inside a GET, treating manager-loop escalation as terminal, rebuilding facts per consumer, launching the LLM before facts commit, or performing lookup-then-launch without atomic same-source reservation would preserve blocking work or duplicate narratives.
- Adversarial negative proof: manager-loop escalation creates no terminal record; reactivation supersedes it; stale claims and capped/missing evidence fail or become explicit partial data; two reclaimed workers create one same-source execution; project-node paging rejects a non-advancing cursor.
- Semantic positive proof: succeeded, failed, and cancelled events seed the correct disposition; a later terminal event revises a reactivated run; subtree facts include steps/repetitions/actors/timing/usage/cost; a completed narrative is connected by execution/manager IDs; terminal nodes still render after canonical runtime details are purged.
- Anti-stub audit: critical tests instantiate the production projector/store/assembler/project adapter and use the production file-backed Agent Framework reservation path for the two-worker race; no canned narrative fallback, fixture-only terminal hook, deep-evidence body copy, TODO, or `NotImplementedException` supplies the result.

## Progression Gate

- SB04 starts only after deterministic record availability is independent of narrative success and terminal project-node behavior passes.

## Reopen Triggers

- Duplicate delivery changes totals; a summary blocks completion/read; manager policy leaks into Application/Persistence; a required API field cannot be derived.

## Suggested Agent Prompt

```text
Implement SB03 only. Use the committed lifecycle seam, persist hard facts before narrative work, preserve escalation resumability, and prove idempotency/failure behavior.
```
