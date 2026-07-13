# Task 03 – Convert safe/idempotent completion failures to bounded automatic current-step rework

## Problem

`ProcessRuntimeEngine.ResultHelpers.cs:203-228` maps every `Blocked` step to:

```csharp
ProcessRecoveryDecisionKind.ManagerRequired
```

This ignores diagnostic metadata:

```json
"retrySafety": "SafeToRetry",
"idempotency": "Idempotent"
```

The enum already has:

- `ProcessRecoveryDecisionKind.SafeRetry`,
- `ProcessRecoveryRouteKind.CurrentStepRetry`.

## Implementation

1. Extract recovery logic into `IProcessRecoveryClassifier`.
2. Add explicit category for product/completion gate failures, e.g. `ProductCompletionGate`.
3. Use diagnostic metadata:
   - all relevant diagnostics `SafeToRetry`,
   - all relevant diagnostics `Idempotent`,
   - no unsafe/policy/approval/denied diagnostic,
   - retry budget not exceeded.
4. Return:

```text
DecisionKind = SafeRetry
RouteKind = CurrentStepRetry
Policy = process.current-step-safe-retry
```

5. In dispatch service, before manager recovery instruction, consume `SafeRetry/CurrentStepRetry` by scheduling/triggering a bounded rework of the same step with a diagnostic repair packet.
6. Keep manager escalation after repeated same fingerprint.

## Retry budget

Add configurable defaults:

- `MaxAutomaticSafeReworksPerStep = 2` or `3`,
- `MaxSameDiagnosticFingerprintAutomaticReworks = 1` or `2`.

The exact value may be config-driven, but tests must cover budget exhaustion.

## Acceptance criteria

For the provided incident child receipt:

```text
process.adapter.product_required_file_content_missing
SafeToRetry
Idempotent
```

with aggregate missing script receipt added by Task 02, recovery decision must be:

```text
SafeRetry / CurrentStepRetry
```

not:

```text
ManagerRequired / ManagerAction
```

Human escalation is allowed only after retry budget exhaustion or non-idempotent/unsafe diagnostics.

## Regression tests

```text
RecoveryClassifier_routes_safe_idempotent_completion_gate_to_current_step_retry
RecoveryClassifier_escalates_after_same_fingerprint_budget_exhausted
RecoveryClassifier_does_not_safe_retry_denied_tool_or_policy_violation
DispatchService_applies_auto_rework_before_manager_instruction_for_safe_retry
DispatchService_does_not_auto_rework_when_route_is_template_repair_or_child_no_go
```

## Notes

Do not solve this by pretending `NeedsManager` is not blocked. It is acceptable for adapter to signal that completion gates failed. The important change is runtime recovery decision and dispatch behavior.
