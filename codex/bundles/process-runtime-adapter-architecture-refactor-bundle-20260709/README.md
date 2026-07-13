# Process Runtime Adapter Architecture Refactor Bundle

Prepared: 2026-07-09

## Purpose

This bundle prepares an architecture-first refactor of the process execution adapter and adjacent runtime/dispatcher integration. It is intentionally not an implementation bundle yet. The implementation must remove the current partial-class expansion pattern around `AgentFrameworkProcessExecutionAdapter`, isolate domain-specific .NET/software-delivery behavior behind process-driver boundaries, and keep generic process runtime/dispatcher/MAF core code free of template or product-domain knowledge.

## Source Analysis Incorporated

- `C:\repositories\CanDoItAll\codex\bundles\tetris-process-rootcause-workflow-bundle-20260709`
- `C:\repositories\CanDoItAll\codex\bundles\escalation_root_cause_bundle`
- Current code snapshot from `CanDoItAll.slnx`
- CodeAnalytics snapshot `snap-20260709171252-c371d5d2`

## Primary Current Findings

1. `AgentFrameworkProcessExecutionAdapter` is a large partial-class cluster, not a real separation of responsibilities. Current inventory shows 20 adapter partial files and more than 6500 lines in the adapter cluster.
2. Generic process integration currently depends directly on .NET setup behavior through `IDotNetSolutionSetupRuntimeExecutor` and `TryExecuteRuntimeOwnedDotNetSetupAsync`.
3. Generic MAF receipt writing currently contains .NET runtime lifecycle special handling in `WorkspaceCommandReceiptWriter.IsDotNetRuntimeLifecycleTool`.
4. Previous fixes improved detection, but left deterministic repair, routing, receipt classification, and domain-specific guidance inside adapter/runtime glue.
5. Pro root-cause analysis shows that branch-aware gates, repair loopbacks, subprocess propagation, typed tool plans, and template/artifact contracts must be solved as architecture boundaries, not more prompt text or adapter conditions.

## Bundle Outcome

After executing this bundle, the expected codebase state is:

- `AgentFrameworkProcessExecutionAdapter` is a thin orchestration adapter or facade only.
- The existing adapter partial files are removed or reduced to a temporary compatibility shell with an explicit deletion gate.
- Completion gates, receipt matching, branch routing, managed artifact materialization, subprocess coordination, recovery packet building, and runtime-owned deterministic driver execution are top-level services with focused tests.
- .NET/software-delivery lifecycle and tool-plan behavior live in driver-owned policy/implementation types, not in generic runtime/dispatcher/MAF receipt writer code.
- Generic runtime and dispatcher accept domain facts only as typed data from templates, process definitions, or drivers.

## Validation Summary

- Bundle preparation status: `Completed`.
- Bundle readiness gate: `Passed`.
- Execution status: `Completed`.
- Subbundle gate review: `Completed`.
- Final closure gate: `Passed`.
- Browser validation analytics: `Not applicable - local proof used unit, source, build, template, and CodeAnalytics validation; no browser UI process path was exercised.`

## Execution Order

1. SB01: Baseline inventory and characterization tests.
2. SB02: Contracts and boundary seams.
3. SB03: Completion gate and receipt pipeline extraction.
4. SB04: Managed artifact and result materialization extraction.
5. SB05: Subprocess and recovery loopback extraction.
6. SB06: Domain driver isolation for .NET lifecycle/tool plans.
7. SB07: Template/artifact audit and final architecture closure.

Critical path: SB01 -> SB02 -> SB03 -> SB06 -> SB07.

## Non-Goals

- Do not implement this bundle during preparation.
- Do not add new partial files to hide growth.
- Do not weaken completion gates or required receipts just to make process runs pass.
- Do not hardcode `qa-validation`, `quality-accepted`, `repair-required`, `.NET`, Tetris, Calculator, or Blazor decisions in generic process runtime/dispatcher code.
- Do not turn domain-specific behavior into broad `Helper`, `Manager`, or `Common` classes.
