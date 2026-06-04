# Process Agent Execution Boundary Foundation v1

## Profile

- `initiative`
- Branch target: `maf-processes-refactor`
- Current upstream branch: `development`
- Prepared date: `2026-06-04`
- Execution status: `Prepared; not implemented`
- Intended executor: Codex
- UI proof policy: **PC / large-screen only**. Do not spend time validating small or medium screens. Do not produce mobile screenshots unless a future user request explicitly changes this scope.

## Purpose

This bundle is the next small-step refactor after `maf-processes-provider-hardening-followup-v1`.

The previous phase successfully moved first-party process, project-structure, and image-generation runtime tools behind registered `IAgentRuntimeToolProvider` providers. MAF is no longer the owner of those product tool builders. The next risk is inside `CanDoItAll.Modules.Processes`: the process dispatcher still knows too much about AgentFramework execution, execution run details, tool receipts, chat/session failure cases, and recovery/adoption behavior.

This bundle prepares and implements a **process agent execution boundary foundation**. It intentionally does **not** split the full process core and does **not** introduce process driver packs.

## Mission

Create a staged boundary between process automation and AgentFramework execution so later `Processes.Contracts` / `Processes.Core` extraction can happen safely.

The desired end state of this bundle is:

1. The previous provider seam remains intact.
2. MAF stays product-tool-neutral and does not regain direct `Processes`, `Projects`, or `Workbench` references.
3. Process dispatcher direct AgentFramework execution calls are reduced behind a process-owned execution client/facade.
4. A minimal contracts/abstractions foundation is introduced only where it lowers future extraction risk.
5. No process driver packs, no full dispatcher rewrite, and no broad process-core extraction are performed yet.
6. Proof remains service/runtime-focused. Browser validation is `N/A` unless a visible UI route is unexpectedly touched; if touched, use large-screen PC proof only.

## Why This Is The Right Next Step

The current branch is much closer to a clean boundary, but a full Process Core split would still be risky because `ProcessRunAutomationDispatchService` directly references AgentFramework Core/Models and invokes `IAgentFrameworkWorkspaceService` in execution paths. Moving core before this seam would force a large DTO and behavior rewrite in one step.

This bundle therefore makes the next low-risk cut: isolate AgentFramework execution coupling in a dedicated process automation execution boundary first.

## Explicit Non-Goals

- Do not extract the full `CanDoItAll.Processes.Core`.
- Do not move EF entities.
- Do not split all dispatcher partials.
- Do not introduce `IProcessDriverPack`.
- Do not add DotNet/Rust/business-analysis drivers.
- Do not change public process tool names.
- Do not change process tool access policy except where this bundle explicitly adds tests to prove current policy.
- Do not run or capture small/medium/mobile UI validation.

## Bundle Contents

- `inputs/` raw request and branch review summary.
- `analysis/` current state, risks, readiness assessment, and core-split decision.
- `requirements/` normalized requirements and hard constraints.
- `architecture/` target execution boundary, staging model, and proof strategy.
- `inventories/` source impact and test impact inventories.
- `plan/` subbundle dependency map with refactor checkpoints.
- `subbundles/` twelve execution-ready subbundles.
- `traceability/` requirement ownership.
- `evidence/checklists/` XLSX execution checklist.
- `reviews/` self-review stubs for execution closure.

## Recommended Execution Order

1. SB01 Entry audit, branch hygiene, and previous provider seam smoke.
2. SB02 Process module dependency and dispatcher coupling inventory.
3. SB03 Execution boundary design and source cutline.
4. SB04 Refactor Gate A: architecture guardrails before movement.
5. SB05 Introduce process automation execution client/facade.
6. SB06 Move direct execution start/detail/adoption/recovery calls behind the facade.
7. SB07 Refactor Gate B: dispatcher coupling reduction proof.
8. SB08 Minimal process contracts/abstractions foundation.
9. SB09 Execution receipt and required-tool projection hardening.
10. SB10 Refactor Gate C: boundary consistency and source-size review.
11. SB11 Runtime smoke, process-filtered integration proof, and large-screen-only validation confirmation.
12. SB12 Final red-team review and next-phase cutline for actual Process Core extraction.

## Validation Summary Required Before Closure

- `dotnet build CanDoItAll.slnx`
- targeted unit tests for provider composition and architecture guards
- process-filtered integration tests
- process outbox / receipt / artifact lineage smoke tests
- hidden dependency scans for MAF product-tool regressions
- source scans proving dispatcher direct AgentFramework execution calls are reduced
- no small/medium/mobile screenshots; browser proof is N/A unless UI changed, and then large-screen PC only
- completed bundle validator
