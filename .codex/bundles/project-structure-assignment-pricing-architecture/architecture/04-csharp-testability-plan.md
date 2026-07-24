# C# Testability Plan

## Characterization

- Existing Gantt projection test remains the canonical proof that person plus AI-agent overlap is valid.
- Preserve existing person/workflow/process quote outputs before moving algorithms.
- Preserve one-assignment stale-update compensation.

## Isolated unit tests

| Owner | Behavior |
| --- | --- |
| assignment resolver | empty, single, mixed, unique primary, explicit ambiguity, input-order independence, unsupported type |
| each cost strategy | source-specific positive and unavailable cases |
| cost dispatcher | exact kind selection |
| estimate refresh service | new/`NotStarted` refresh, missing quote clearing, historical preservation, `Unknown` fail closed |
| execution-state policy | legal forward transitions, timestamp invariants, backward-transition rejection |
| canvas task coordinator | create/edit delegation and authoritative pricing without page construction |

## Negative/shallow-pass tests

- mixed assignments do not throw and are all present in the resolution.
- duplicate or missing strategy for a kind throws an explicit configuration error.
- Agent selection never calls the CRM person strategy.
- missing CRM rate removes a stale amount/currency for an unstarted task.
- mixed direct assignment clear/change is unavailable and unchanged field saves retain both assignments.
- started task does not call the estimator.
- legacy `Unknown` task does not call the estimator.

## Composition/integration smoke

- full module DI resolves one strategy per resource kind.
- Gantt coordinator opens the dialog for mixed assignments.
- task creation persists the strategy quote, not submitted stale cost.
- task details update applies lifecycle-aware quote before mutation.
- canvas create/edit submission refreshes through the shared policy or an equivalent server/submission boundary.

## Fakes

- CRM cost-rate bridge.
- workflow run/usage stores.
- process historical-cost reader/catalog.
- AgentFramework usage analytics or its narrow adapter input.
- task quote dispatcher/strategy for lifecycle policy tests.

## Integration-only proof

- real EF-backed CRM mixed assignment storage and single-assignment compensation.
- rendered Radzen/shared-component dialog behavior.
- local-host Gantt interaction.

## Required transcripts/results

Behavioral evidence is recorded in `reviews/01-execution-report.md` with exact test names and commands. Separate manifests/hashes are not required because no subbundle is Governed.
