# Tool Validation Rule Family Inventory Template

Codex must refresh this from live source in SB02.

| Method/Region | Rule family | Inputs | Output | Side effects | Candidate helper | Existing tests |
| --- | --- | --- | --- | --- | --- | --- |
| `ResolveRequiredToolNames` | required-tool discovery | candidate | tool names | none expected | `ProcessRequiredToolValidationRules` | TBD |
| `ResolveMetadataRequiredToolNames` | metadata required browser proof | candidate/detail | tool names | none expected | `ProcessRequiredToolValidationRules` | TBD |
| `ResolveMissingRequiredToolExecutions*` | missing required tools | candidate/detail/prior tools/carried proof | missing names | none expected | `ProcessRequiredToolValidationRules` | TBD |
| `ResolveProcessMockSatisfiedToolNames` | process mock equivalent evidence | candidate/detail/required tools | satisfied tool names | none expected | `ProcessRequiredToolValidationRules` | TBD |
| `CanSatisfyMissingDotnetNewWithValidatedExistingScaffold` | dotnet scaffold equivalence | detail | boolean | none expected | `ProcessRequiredToolValidationRules` or stack-specific local helper | TBD |
| `ResolveUnresolvedCriticalToolFailures*` | critical failure resolution | detail/candidate | receipts | none expected | `ProcessCriticalToolFailureRules` | TBD |
| `ShouldIgnoreStackInapplicableCriticalToolFailure` | stack-specific suppression | candidate/receipt | boolean | none expected | local stack compatibility helper; not driver API | TBD |
| `ResolveCompletionStatusWithCarryForward*` | completion status decision | candidate/detail/outcome/facts | step status | none expected | `ProcessCompletionDecisionRules` | TBD |
| `BuildCompletionReasonWithCarryForward*` | completion reason | candidate/detail/facts | string | none expected | later helper if safe | TBD |
| retry/rework helpers | recovery decision facts | candidate/detail/facts | decision facts | persistence elsewhere | `ProcessRecoveryRetryDecisionRules` | TBD |
