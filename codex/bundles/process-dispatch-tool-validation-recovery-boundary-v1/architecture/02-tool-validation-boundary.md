# Tool Validation Boundary

## Snapshot Types

Codex should introduce only local snapshot/fact records where they reduce coupling:

- `ProcessToolValidationSnapshot`
- `ProcessToolReceiptFact`
- `ProcessRequiredToolSet`
- `ProcessRequiredToolDecision`
- `ProcessCriticalToolFailureDecision`
- `ProcessCompletionBlockerSummary`
- `ProcessCompletionDecisionInput`
- `ProcessCompletionDecision`

Snapshots must not contain EF entities, `DbContext`, storage services, MAF runtime types, or dispatcher mutable collections.

## Required Tool Rule Families

- declared required tools from process/work brief rules,
- metadata required browser proof tools,
- process mock artifact substitution,
- dotnet scaffold substitution,
- carried implementation proof substitution,
- current-attempt-only implementation/browser proof restrictions,
- workspace-write satisfaction via recorded artifacts,
- implementation validation tools.

## Critical Failure Rule Families

- latest critical receipt grouping,
- failed receipt detection,
- superseded failure suppression,
- stack-inapplicable dotnet failure suppression,
- unrecoverable required tool classification.

## Completion Decision Families

- non-terminal run status,
- pending approvals,
- failed/succeeded outcome,
- declared outcome status and context validation,
- blocker summaries,
- missing tools,
- unresolved critical failures,
- implicit completion allowance,
- governed step fallback failure.
