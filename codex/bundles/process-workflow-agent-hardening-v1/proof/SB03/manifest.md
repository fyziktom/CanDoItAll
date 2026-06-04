# SB03 Proof Manifest

## Status

Passed. SB03 replaced metric-only token/cost reporting with durable provider usage observations, known/unknown usage statuses, ledger-first process cost aggregation, workflow usage summaries, and persistence/invariant support.

## Delivered Changes

- Added `ProviderUsageObservation`, `ProviderUsageObservationStatus`, `ProviderUsageSourcePhases`, and `ProviderUsageSummary`.
- Added usage observations to runtime responses, runtime usage exceptions, structured-output repair results, execution run details, workspace documents, and execution state.
- Captured usage from MAF runtime success, finalizer short-circuit/recovery, failure-after-provider-call, structured-output repair, workflow LLM invocation, and legacy metrics.
- Persisted usage observations in file-backed execution slices, including run-scoped and orphan usage roots.
- Updated pricing and process actual cost aggregation to distinguish known, estimated, unavailable, and missing usage.
- Added tests covering normal run usage, finalizer short-circuit usage, failed-after-provider-call usage, structured-output repair usage, workflow known/unknown summaries, process ledger-first cost, and pricing status semantics.

## Command Transcripts

- `proof/SB03/transcripts/failing-first-undercount-mutation.txt`
- `proof/SB03/transcripts/provider-pricing-usage-tests.txt`
- `proof/SB03/transcripts/workflow-llm-usage-tests.txt`
- `proof/SB03/transcripts/execution-and-process-usage-ledger-tests.txt`
- `proof/SB03/transcripts/drift-scanner-after-sb03.txt`
- `proof/SB03/transcripts/prepared-validator-after-sb03.txt`
- `proof/SB03/transcripts/prepared-validator-after-sb03-final.txt`
- `proof/SB03/transcripts/git-diff-check-after-sb03.txt`
- `proof/SB03/transcripts/source-assertions.txt`
- `proof/SB03/transcripts/anti-stub-audit.txt`
- `proof/SB03/transcripts/compile-errors-initial.txt`

## Shallow-Pass Trap

The tests do not only assert that a cost field is non-zero. They assert semantic source/status behavior:

- Finalizer short-circuit with zero legacy metrics must preserve `MissingAfterProviderActivity`.
- Failure-after-provider-call must preserve an observed runtime usage observation, not fall back to `EstimatedFromMetric`.
- Process cost must prefer ledger observations and ignore legacy metrics for details that already have ledger entries.
- Unknown workflow usage must set unknown observation counts and `HasUnknownUsage`.

## Adversarial Negative Proof

`proof/SB03/transcripts/failing-first-undercount-mutation.txt` temporarily disabled runtime-response usage preservation and runtime usage exception handling in `AgentFrameworkWorkspaceExecutionService.Usage.cs`. The targeted finalizer and failure-after-provider-call tests failed:

- Finalizer expected `MissingAfterProviderActivity` but got `ObservedFromMetric`.
- Failure-after-provider-call expected `Observed` but got `EstimatedFromMetric`.

The mutation was reverted before closure, and the targeted integration slice passed afterward.

## Semantic Positive Proof

Passing targeted slices:

- `ProviderPricingTests`: 6 passed, including known-vs-estimated usage summary behavior.
- Workflow LLM usage tests: 6 passed, including known provider observations and unavailable usage.
- Execution/process usage ledger tests: 6 passed, covering normal run, runtime failure after provider call, structured-output repair, invalid structured output failure, finalizer short-circuit missing usage, and process ledger-first cost.
- Process contract drift scanner: 6 passed after SB03 to confirm SB01 invariants were not broken.
- Prepared bundle validator: PASS after SB03.
- `git diff --check`: exited cleanly; transcript contains only Git CRLF normalization warnings.

## Source Assertions

`proof/SB03/transcripts/source-assertions.txt` confirms the production source contains `UsageObservations`, `ProviderUsageObservation`, `AgentRuntimeUsageException`, `MissingAfterProviderActivity`, `StructuredOutputRepair`, `ResolveProcessRunActualCost`, and workflow usage summary paths.

## Anti-Stub Audit

`proof/SB03/transcripts/anti-stub-audit.txt` found only test fixture strings, including the Tetris scenario payload and a `TODO register` fixture in process dispatch tests. No SB03 production behavior depends on placeholder or stubbed production logic.

## Raw Note Literal Closure

- OpenAI token/cost mismatch: closed for internal accounting by making provider usage durable and unknown usage explicit. External OpenAI dashboard reconciliation remains pending because no billing export/API is available locally.
- Process actual cost undercount: closed by ledger-first process cost aggregation and mutation-backed tests.
- Finalizer zero-token risk: closed by observed-or-missing usage observations and finalizer short-circuit test.
- Failed-run estimated-token risk: closed by runtime usage exception preservation and failure-after-provider-call test.
- Structured-output repair usage: closed by repair usage observations linked to parent execution run.
- Workflow summarization usage: closed for workflow DTOs/metrics; process cost still excludes upstream workflow usage unless explicitly correlated.

## Additional Artifacts

- `proof/SB03/semantic-invariants.md`
- `proof/SB03/changed-file-hashes.md`
- `proof/SB03/production-behavior-artifact-matrix.md`
- `proof/SB03/tetris-reconciliation.md`
