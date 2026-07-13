# Structured Input

## Problem Statement

Nested and subprocess-heavy CanDoItAll process runs can block and then repeat manager rework because the runtime loses the exact diagnostic, cannot deterministically bridge accepted child handoff artifacts into parent produced slots, exposes slot GUIDs instead of semantic artifact contracts, checks agent readiness instead of exact composed runtime tools, and leaves important template gates in prose.

The concrete observed example is `prepare-solution-skeleton` in `.NET implementation slice with atomic validation`, still blocked in the 5032 instance when this bundle was requested. The product output may exist, but the managed process evidence contract is not closed. This bundle must repair the entire failure class.

## GPTPro Findings To Preserve

- F01: operator diagnostics lose the true blocker when AgentFramework observation is missing.
- F02: observation query is run-level, not step-level.
- F03: subprocess orchestration ownership is split between runtime and normal agent prompts.
- F04: completed child evidence is generic and not validated against accepted child output contracts.
- F05: artifact ledger can use original command result instead of applied finalization result.
- F06: runtime contract prompt exposes GUID slots instead of semantic artifact descriptors.
- F07: produced artifact identity and hashes are not grounded in managed artifact content.
- F08: readiness checks do not preflight exact composed runtime tools.
- F09: `prepare-solution-skeleton` has manual skip and prose-only accepted repair handoff.
- F10: manager rework quality is too generic.
- F11: template hard gates are buried in long prose.
- F12: process/MAF classes are large and require responsibility-based extraction.

## Scope Expansion From Local Audit

The current repository contains nine subprocess parent steps:

| Parent process | Step | Child process | Risk |
| --- | --- | --- | --- |
| `dotnet-development-slice` | `prepare-solution-skeleton` | `dotnet-solution-setup` | Manual skip enabled; repair handoff and no-go are prose-only. |
| `dotnet-development-slice` | `implement-code-change` | `dotnet-feature-function-implementation` | Accepted repaired handoff and no-go escalation are prose-only. |
| `dotnet-development-slice` | `slice-repair-code-change` | `dotnet-feature-function-implementation` | Repair acceptance/no-go semantics are prose-only. |
| `software-delivery` | `architecture-review` | `dotnet-architecture-design-review` | Parent maps two child artifacts but lacks typed terminal contract. |
| `software-delivery` | `implementation` | `dotnet-development-slice` | Parent accepts slice handoff and repaired handoff in prose. |
| `software-delivery` | `capture-ui-screenshots` | `dotnet-ui-screenshot-writeback` | Parent requires child handoff plus visual-analysis receipts; typed semantics missing. |
| `software-delivery` | `capture-ui-screenshots-after-repair` | `dotnet-ui-screenshot-writeback` | Repaired screenshot handoff semantics missing from metadata. |
| `software-delivery` | `record-runtime-commands` | `dotnet-runtime-command-writeback` | Parent requires handoff plus command-node receipts; typed semantics missing. |
| `software-delivery` | `record-runtime-commands-after-repair` | `dotnet-runtime-command-writeback` | Repaired runtime-command handoff semantics missing. |

## Assumptions

- Runtime-owned subprocess handling should be the primary model for `StepKind=Subprocess` with `SubprocessProcessKey`.
- Agent-owned launch via `project_structure_process_subprocess_launch` may remain as a compatibility/manual fallback but must not be the default control path for controlled templates.
- New typed template metadata can be additive first, preserving old fields such as `SubprocessChildStepKey` until migration is complete.
- Current CodeAnalytics evidence is sufficient for preparation but must be refreshed after implementation because project references or large-class ownership may change.

## Required Proof Themes

- Exact process run and step id observation lookup.
- Runtime receipt fallback when AgentFramework observations are unavailable.
- Structured process result JSON persisted for process-bound AgentFramework runs.
- Typed subprocess contract with accepted and no-go child outputs.
- Parent synthesized managed artifacts tied to parent produced slots.
- Produced artifact identity and ledger events grounded in applied results and actual managed content.
- Exact runtime tool preflight before agent execution.
- Template validation across all affected subprocess parents and shared artifact contracts.
- Unit/integration tests that fail on the current fragile behavior and pass after implementation, without live LLM or network dependency.
