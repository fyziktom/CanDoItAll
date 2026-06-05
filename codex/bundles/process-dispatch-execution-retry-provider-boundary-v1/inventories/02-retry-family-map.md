# Retry Decision Family Map

| Family | Current source | Future local helper | Driver-readiness only? |
| --- | --- | --- | --- |
| Incomplete successful run retry | `Concurrency.cs` | `ProcessIncompleteSuccessfulRunRetryRules` | Yes, document only |
| Recoverable failed run retry | `Concurrency.cs` | `ProcessRecoverableFailedRunRetryRules` | Yes, document only |
| No-progress compression | `Concurrency.cs` | `ProcessNoProgressRetrySignalBuilder` + journal coordinators | Yes, document only |
| Provider fallback recovery | `Execution.cs` | `ProcessProviderRepairCoordinator` | Yes, document only |
| Historical carried proof | `Execution.cs` | `ProcessHistoricalCarriedProofQueryCoordinator` | Yes, document only |
