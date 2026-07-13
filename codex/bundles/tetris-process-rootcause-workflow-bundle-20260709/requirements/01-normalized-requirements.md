# Normalized Requirements

## R01 Branch-Aware Receipt Rules

Required tool receipt rules must support branch outcome applicability, purpose, current-run requirement, successful-exit policy, minimum count, and reason. Legacy newline strings, JSON string arrays, and by-step string maps must keep working.

## R02 Branch-Aware Gate Enforcement

Completion gates must evaluate only receipt rules applicable to the current branch outcome and must record skipped rules with reasons. `quality-accepted` can require acceptance UI proof. `repair-required` and repair-escalation branches must not require acceptance-only UI proof when deterministic defect evidence exists.

## R03 Completion Issue Routing

Completion gate issues must be able to route to current-step retry, branch outcome, or manager action. Product content/readback failure on an accepted branch must route to template-defined repair branch when metadata exists.

## R04 Runtime Gate Findings

When runtime changes or confirms a branch route because of completion gate issues, it must persist or append runtime gate findings that downstream repair steps can read. The findings must include original branch, routed branch, issue code, safe product-relative references, current execution run id, and receipt applicability summary.

## R05 Receipt Deduplication

Product completion receipt rules and capability scope receipt rules must not produce duplicate diagnostics for the same semantic requirement. Prefer separating tool exposure from completion evidence obligations.

## R06 Generic Boundary Repair

Generic process application/runtime/dispatcher code must not contain .NET, Blazor, Tetris, scaffold file, QA step, or software-delivery branch constants as behavior. Domain recovery advice must move behind provider/template metadata.

## R07 Template Migration Coverage

All process templates with accepted/repair validation branch flows or required receipt gates must be inventoried, migrated, or explicitly exempted. This includes software-delivery, Blazor delivery/repair variants, dotnet feature/function implementation, dotnet development slice, dotnet solution setup, and dotnet UI screenshot writeback.

## R08 Artifact And Acceptance Matrix

Project-structure requirements must be materialized as a machine-readable acceptance criteria matrix. Implementation, review, QA, repair, and recheck steps must map proof to criteria ids so a shell UI cannot satisfy complex behavior requirements.

## R09 .NET Runtime Tool Lifecycle

The .NET workspace tool layer must record startup owner metadata, product root, project path, URL, process ids, startup receipt path, and cleanup receipt. Stop must be idempotent. Stale app/process ownership must lead to actionable diagnostics or explicit safe orphan cleanup, not generic process escalation.

## R10 Observability And Operator UX

Diagnostics must show applicable gates, skipped gates, observed receipt state, issue route decisions, branch target, and final route. Operator UI must not collapse branch routing into a generic `NeedsManager` summary.

## R11 Regression Proof

Tests must cover the exact incident combinations and broader branch/receipt cases without an LLM:

- `quality-accepted` plus full receipts plus scaffold defect routes repair branch;
- `repair-required` plus deterministic defect skips acceptance-only receipts;
- repair branch without defect evidence and without proof is not accepted;
- branch-routable defects do not consume same-step retry budget;
- no domain hardcodes remain in generic runtime/application code.
