# Target Solution

## Local Boundary Shape

The target is a local process-module boundary, not a public core API.

```text
ProcessRunAutomationDispatchService.ToolValidation.cs
  -> ProcessToolValidationSnapshotBuilder
  -> ProcessToolReceiptFacts
  -> ProcessRequiredToolValidationRules
  -> ProcessCriticalToolFailureRules
  -> ProcessCompletionBlockerRules
  -> ProcessCompletionDecisionRules
  -> ProcessRecoveryRetryDecisionRules
```

The dispatcher remains responsible for:

- loading candidate/run/step state,
- invoking execution client,
- persisting recovery journals,
- mutating assigned providers,
- transitioning step status,
- recording artifacts,
- logging operator-facing runtime details.

Helpers may own:

- pure string/tool-name normalization,
- receipt grouping,
- missing-tool classification,
- critical-failure filtering,
- equivalent evidence decisions,
- status/reason decision tables,
- driver-readiness documentation categories.

## Desired End State

At the end:

- `ToolValidation.cs` delegates at least required-tool and critical-failure decisions to local helpers.
- Completion status decision logic is partly organized behind a typed fact collection layer.
- No production behavior is intentionally changed.
- Tests prove current special cases still work.
- A future bundle can safely continue with completion/finalizer/recovery separation or revisit Process Core readiness.
