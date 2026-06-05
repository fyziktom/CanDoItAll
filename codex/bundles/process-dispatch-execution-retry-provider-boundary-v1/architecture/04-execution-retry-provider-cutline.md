# Execution Retry Provider Cutline

## Pure Rules

- `ProcessExecutionResponseTextResolver`: response text selection from result summary, chat session, and serialized session state.
- `ProcessExecutionAttemptRequestBuilder`: immutable request, source, policy, and metadata construction.
- `ProcessExecutionPostAttemptFacts`: current attempt facts used by completion and retry decisions.
- `ProcessExecutionRetryDecisionRules`: retry decision families and reason aggregation.
- `ProcessNoProgressRetrySignalBuilder`: no-progress fingerprint inputs and hash construction.
- `ProcessRecoverableProviderFailureRules`: provider failure text detection and classification.
- `ProcessProviderFallbackRules`: fallback provider ordering and editor model normalization.

## Explicit Side-Effect Coordinators

- `ProcessConcurrentExecutionAdoptionCoordinator`: lists execution runs, polls detail, and returns an adoption snapshot.
- `ProcessRecoveredExecutionAdoptionCoordinator`: loads the recoverable execution run and normalizes response text.
- `ProcessExecutionAttemptLauncher`: invokes `executionClient.ExecuteRunAsync` and normalizes failed launch detail.
- `ProcessNoProgressRetryJournalCoordinator`: queries and writes no-progress retry ledger records.
- `ProcessProviderRecoveryCoordinator`: lists agents/providers, probes health, loads assignments, and saves repaired agents.
- `ProcessExecutionLoopFacade`: keeps the high-level attempt loop readable without hiding side effects.

## Reopen Triggers

- A helper introduces `CanDoItAll.Processes.Core`, `IProcessDriverPack`, driver registry, or driver package naming.
- A helper with side effects is presented as a pure rule class.
- Retry counts, provider fallback ordering, no-progress fingerprint inputs, journal event names, or typed recovery directive shape drift.
- `ExecuteUntilSettledAsync` loses recover/adopt/launch/observe/repair/retry ordering.

## Closure

- SB03 creates no production behavior change.
- This cutline is the reference for helper naming and side-effect classification in SB05-SB40.
