# Target Solution

## End State

The process runtime can distinguish:

- incomplete same-step proof;
- deterministic product defects that should route a repair branch;
- true manager/action escalation.

Receipt rules are branch-aware and purpose-aware. Completion issues are routed by generic metadata. Domain-specific .NET, Blazor, scaffold, QA, and software-delivery knowledge lives in Workbench contributors, process templates, or recovery advice providers rather than generic process application/runtime code.

## Main Components

- `ProcessCompletionGateEvaluator`: pure application/runtime service that evaluates completion gates from assignment, output, receipts, product access, branch outcome, rule set, and route metadata.
- `ProcessCompletionReceiptRuleResolver`: normalizes legacy strings, JSON string arrays, JSON object arrays, and by-step maps into typed receipt rules.
- `ProcessRequiredToolReceiptEvaluator`: counts receipts, checks current-run and success policy, and returns matched/missing/skipped rule facts.
- `ProcessCompletionIssueRouter`: maps completion issues to current-step retry, branch outcome, or manager action using template/process metadata.
- `ProcessCompletionEvaluationTrace`: durable trace of applicable/skipped rules, observed receipts, issue route decision, and branch target.
- `IProcessRecoveryAdviceProvider`: provider boundary for generic, .NET software-delivery, subprocess, and future domain-specific recovery advice.
- Acceptance criteria matrix artifact: generic project-structure-derived artifact used by implementation, review, QA, repair, and recheck.

## Boundary Principles

- Contracts and abstractions may define branch outcome keys as data, receipt purpose, route kind, issue code, and trace records.
- Generic runtime/application code may not interpret `qa-validation`, `quality-accepted`, `repair-required`, `.NET`, `Blazor`, `Counter.razor`, `Weather.razor`, or `Tetris`.
- Workbench/software-delivery templates may define those domain terms and emit structured metadata.
- Composition root wires providers; core behavior does not use service location.

## Compatibility

- Legacy receipt strings remain valid.
- Legacy by-step string maps remain valid.
- Missing route metadata preserves current manager/retry behavior.
- New structured object rules can be gradually adopted by templates.

## Required Proof Shape

- Failing-first incident regression before behavior change.
- Passing incident regression after routing implementation.
- Unit tests for parser compatibility and branch filtering.
- Architecture test scanning generic code for forbidden domain constants.
- Source assertions showing domain constants moved out of generic application/runtime code.
- Template inventory closure proving every similar process template was migrated or explicitly exempted.
