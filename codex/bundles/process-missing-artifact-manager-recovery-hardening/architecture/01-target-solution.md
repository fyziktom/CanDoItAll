# Target Solution

## Minimal Design

Keep the missing-artifact gate inside `ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs`, because it already runs after artifact projection and before step transition.

Change the recovery action from same-executor `ExecuteUntilSettledAsync(candidate)` to manager-mediated recovery:

1. Resolve the process manager technical agent from the process run manager id/name and manager-like fallback options.
2. Record a manager directive journal entry describing the missing artifact recovery request.
3. Execute a single targeted recovery run using the manager technical agent and the existing governed process-step prompt path.
4. Project the manager recovery execution artifacts against the original step expectations.
5. Complete only when all required artifact expectation ids are recorded; otherwise block with exact remaining artifact titles.

## Boundaries

- Artifact expectation matching remains in the existing artifact projection code.
- The manager recovery prompt must not ask for broad implementation work.
- The implementation must not add a new persistence concept unless the existing journal entries are insufficient.
- UI changes are out of scope.
