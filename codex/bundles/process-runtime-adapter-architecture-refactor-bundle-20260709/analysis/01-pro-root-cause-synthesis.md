# GPTPro Root-Cause Synthesis

This bundle incorporates the root causes from the referenced GPTPro analysis bundles. The synthesis below maps those root causes to architecture work, not just behavioral fixes.

## Branch And Receipt Root Causes

GPTPro found that required receipt gates were not correctly branch-aware. For example, acceptance proof receipts should block a `quality-accepted` branch, but should not necessarily block a `repair-required` branch when a deterministic defect has already been proved.

Architecture implication:

- Receipt rules must be typed and branch-aware.
- Receipt rule parsing and matching must be separated from adapter result conversion.
- Completion issue routing must be a generic policy pipeline driven by process definition/template/driver metadata.
- Generic runtime must not hardcode branch keys such as `quality-accepted` or `repair-required`.

## Completion Gate Root Causes

GPTPro found that completion gate failures were treated too often as same-step retry or manager escalation instead of branch-routable process evidence.

Architecture implication:

- Completion gate evaluation must return ordered issues plus route candidates.
- Branch routing must be a distinct service, not hidden inside `ToAdapterResult`.
- Runtime gate findings must become explicit evidence and must be visible to downstream steps.
- Safe/idempotent gate failures must preserve enough structured metadata for recovery routing.

## Repair Loopback Root Causes

GPTPro found that prior repairs improved detection but did not reliably convert detection into deterministic repair:

- Agent got plans as prompt text only.
- Rework was too generic.
- Missing tool-plan receipts were hidden behind the first detected file-content failure.
- Safe/idempotent diagnostics could still escalate to manager.

Architecture implication:

- Tool plans must be typed data or driver-owned plan contracts.
- Recovery instruction building must be diagnostic-specific and separately testable.
- Gate evaluation must aggregate issues instead of short-circuiting.
- Recovery classifier must use typed diagnostic metadata and budgets, not substring checks.

## Subprocess Root-Cause Propagation

GPTPro found that parent process steps can lose child root cause. File existence was sometimes treated as evidence even when runtime gates had rejected the child artifact.

Architecture implication:

- Child run state resolution must be a separate typed service.
- Parent bridge must prefer accepted artifact ledger/slot data.
- File existence fallback must be explicit recovery mode with diagnostic evidence.
- Parent diagnostics must include actionable child diagnostic codes and safe summaries.

## Domain Boundary Root Causes

GPTPro explicitly marked .NET/tool/domain knowledge in generic process layers as leakage. The earlier bundles listed examples such as hardcoded QA step keys, .NET tool names, Blazor/Tetris/scaffold checks, and software-delivery branch names in generic code.

Architecture implication:

- Generic runtime may contain process ids, step ids, branch outcome keys as data, generic branch signals, generic receipt matching, generic file/readback abstractions, retry/manager policies, and diagnostics.
- Generic runtime must not contain .NET tool names, Blazor scaffold words, Tetris or Calculator terms, software-delivery step keys, or branch keys hardcoded in logic.
- .NET/software-delivery knowledge belongs to templates, process drivers, Workbench contribution, or domain driver implementations.

## Template And Artifact Root Causes

GPTPro found that process templates and artifact templates can encode ambiguous contracts. The Tetris incident is one example, but the same shape can affect other process templates.

Architecture implication:

- This bundle includes an audit phase for all process templates and artifact templates touched by these runtime contracts.
- The audit must distinguish allowed template domain terms from forbidden generic runtime/domain terms.
- Template migration must prefer typed fields and structured execution contracts over more prompt prose.

