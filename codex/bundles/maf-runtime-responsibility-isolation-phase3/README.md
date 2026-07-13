# MAF Runtime Responsibility Isolation Phase 3

This initiative bundle prepares the next MAF runtime refactor phase. It exists because the previous phase removed visible `MafAgentRuntime` partial files, but the architecture is still not truly isolated: the runtime remains a large execution coordinator, and large hidden seams now live in `RuntimeCapabilityComposer`, `MafRuntimeAgentFactory`, and `WorkspaceRuntimePlugin`.

## Profile

- `initiative`

## Mission

Make `MafAgentRuntime` a thin `IAgentRuntime` adapter with explicit, independently testable runtime collaborators for turn orchestration, provider streaming, finalizer repair, session persistence, approval continuation, hosted-agent construction, capability composition, and workspace tool families. This bundle is generic MAF runtime architecture work only.

## Outcome Contract

- Requested outcome: prepare an implementation-ready follow-up bundle using the C# modular refactoring skills; do not implement production changes in this turn.
- Hard constraints: no Financial Strategist, quotation, margin, MarkItDown, or other domain-agent behavior; no new partial-class final boundary; no `Helper`, `Utils`, `Common`, or broad `Manager` dumping ground; no service-locator shortcut in core runtime behavior; preserve existing behavior unless a subbundle records an explicit compatibility exception.
- Evidence required before closure: CodeAnalytics-backed before/after snapshots, direct unit tests for each extracted owner without constructing `MafAgentRuntime`, composition smoke for DI/runtime wiring, source assertions proving moved behavior no longer lives in the old large type, partial-class policy proof, and project dependency proof if references change.
- Known blockers or explicit scope exceptions: this bundle does not repair unrelated full-suite failures or unrelated package advisories. `McpCapabilityBuilder` and `ToolCapabilityBuilder` are named as downstream hotspots, but this phase only changes them when required by capability-composer decomposition.

## Root Cause

The root problem is not one missing PDF or spreadsheet tool. The root problem is that core runtime responsibilities are concentrated in a few types that are hard to instantiate, hard to fake, and hard to extend without editing MAF internals.

- `MafAgentRuntime` still owns execution flow, finalizer repair, session serialization, approval continuation, provider usage diagnostics, background continuation, and direct collaborator construction.
- `RuntimeCapabilityComposer` is still a partial class cluster and owns access planning, descriptor construction, capability attachment orchestration, runtime tool provider filtering, workspace/plugin creation, compaction, and path resolution.
- `MafRuntimeAgentFactory` owns runtime build orchestration plus handoff build, tool instrumentation, script policy inspection, finalizer tool capture, credential environment promotion, and chat history construction.
- `WorkspaceRuntimePlugin` exposes workspace file, git, dotnet, script, document, spreadsheet, image, access policy, external-target normalization, and image-analysis behavior in one plugin type.
- Current tests improved, but many still exercise behavior through the large composer or full runtime. That prevents fast unit tests and makes architecture regressions easy.

## Evidence Baseline

- CodeAnalytics snapshot: `snap-20260706180906-6ece4834`, scoped to `CanDoItAll.AgentFramework.Maf`.
- CodeAnalytics hotspots: `RuntimeCapabilityComposer` 106 source members, `WorkspaceRuntimePlugin` 93, `MafAgentRuntime` 59, `MafRuntimeAgentFactory` 31.
- Local line counts: `Runtime/MafAgentRuntime.cs` 1779 lines, `Runtime/Capabilities/RuntimeCapabilityComposer.cs` 972 lines plus partial files, `Runtime/Workspace/WorkspaceRuntimePlugin.cs` 922 lines, `Runtime/MafRuntimeAgentFactory.cs` 886 lines.
- Microsoft Learn grounding: [.NET DI guidelines](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection/guidelines#recommendations) warn against service locator and hard-coded dependency patterns; [.NET unit testing best practices](https://learn.microsoft.com/dotnet/core/testing/unit-testing-best-practices#best-practices) require unit tests to avoid infrastructure dependencies.

## Bundle Layout

- `inputs/` raw request, source artifacts, and structured input.
- `analysis/` current state, root cause, assumptions, risks, and reopen triggers.
- `requirements/` normalized, testable requirements.
- `architecture/` C# current-state inventory, boundary map, dependency direction, pattern records, and testability plan.
- `inventories/` responsibility and large-type inventories.
- `plan/` execution order, dependency graph, phase gates, and architecture checkpoints.
- `traceability/` requirement-to-subbundle mapping.
- `shared-prompts/` reusable implementation and QA prompts.
- `subbundles/` numbered execution-ready workstreams.
- `proof/` planned proof manifests and semantic-invariant skeletons.
- `reviews/` self-review, execution-report seed, and C# architecture gate seed.

## Recommended Execution Order

1. `subbundles/01-current-state-and-characterization`
2. `subbundles/02-turn-coordinator-and-runtime-facade`
3. `subbundles/03-streaming-finalizer-session-drivers`
4. `subbundles/04-runtime-agent-factory-decomposition`
5. `subbundles/05-capability-composer-decomposition`
6. `subbundles/06-workspace-tool-family-extraction`
7. `subbundles/07-project-boundary-and-di-hardening`
8. `subbundles/08-architecture-guards-and-final-proof`

## Dependency And Validation Map

- Keep `plan/01-phase-plan.md` as the source of truth for sequencing and gates.
- SB01-SB05 are critical foundations. SB06 depends on the capability seam, and SB07-SB08 cannot start until the old runtime/composer/factory/plugin responsibilities have real extracted owners.
- Every extracted collaborator must have a direct unit test that does not instantiate `MafAgentRuntime`.
- Any project reference change must include a before/after dependency table and a refreshed CodeAnalytics dependency/cycle result.
- Browser validation is `N/A` unless an implementation subbundle adds browser-visible diagnostics. Host-visible proof is required for workspace process/tool behavior touched by SB06.

## Validation Summary

- Bundle preparation status: `Prepared; validate_bundle.py --stage prepared passed`
- Execution status: `Partial implementation completed; pass with follow-up required`
- Subbundle gate review: `SB08 proof updated for cross-subbundle partial implementation`
- Final closure gate: `Not fully closed; residual MAF hotspots remain`
- Browser validation analytics: `N/A for backend architecture unless UI-visible diagnostics are added`
- Focused validation: MAF build passed; focused MAF unit slice passed 56/56; `MafAgentRuntimeHandoffTests` passed 3/3.
- Full unit project: failed with unrelated existing failures, 13 failed and 1791 passed; see `proof/SB08/transcripts/full-unit-tests.txt`.
- Final CodeAnalytics: `snap-20260706191451-275f822a`, dependency cycles `[]`; `WorkspaceRuntimePlugin` reduced to 964 lines/89 members; `RuntimeCapabilityComposer` and `MafAgentRuntime` remain follow-up hotspots.
