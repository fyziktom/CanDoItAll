# Task 05: Add branch-routable completion issues

## Goal

Some completion gate failures are not manager cases and not same-step retries. They are valid branch routing decisions.

## Required model change

Extend completion issue/evaluation with routing metadata. Suggested values:

```csharp
public enum ProcessCompletionIssueRouteKind
{
    CurrentStepRetry,
    BranchOutcome,
    ManagerAction
}
```

Add fields such as:

- `RouteKind`,
- `SuggestedBranchOutcomeKey`,
- `RoutingSummary`,
- `EvidenceRefsToAdd` or runtime gate findings ref.

## Routing metadata source

Do not hardcode branch names. Add template/process metadata, for example:

```json
"CompletionIssueRoutes": [
  {
    "StepKey": "qa-validation",
    "IssueCode": "process.adapter.product_required_file_content_missing",
    "WhenBranchOutcomeKeys": ["quality-accepted"],
    "RouteKind": "BranchOutcome",
    "TargetBranchOutcomeKey": "repair-required"
  }
]
```

If metadata is not available, keep current behavior for backward compatibility.

## Runtime gate findings

When runtime changes branch routing, persist a small gate finding artifact or append to primary managed artifact. It must include:

- original branch outcome,
- routed branch outcome,
- issue code,
- product check summary,
- affected file aliases or safe product-relative refs,
- current execution run id.

## Acceptance

- `quality-accepted + scaffold content failure` routes to repair branch and does not consume retry budget.
- Downstream repair can read why repair branch was selected.
- No `.NET` or software-delivery branch names appear in generic routing logic.
