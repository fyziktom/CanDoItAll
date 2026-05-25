# Upstream Materialization And Unblock Lifecycle

## Problem

When a downstream step is missing an upstream artifact, current logic blocks the downstream step and asks the source step to rerun. The downstream step can remain blocked after the upstream artifact is produced.

## Proposed Lifecycle

1. Downstream step detects missing artifact input.
2. Runtime records `upstream-artifact-materialization-requested`.
3. Downstream step enters a resumable waiting state or blocked-with-dependency state.
4. Source step is rerun or manager recovery is invoked.
5. When source artifact is recorded, process runtime evaluates dependent steps.
6. Downstream step is moved back to `Ready` or `WaitingApproval` when all mandatory inputs are now present.
7. Journal records `upstream-artifact-materialization-satisfied`.

## Minimal Schema Option

If adding a new status is too broad, keep `Blocked` but add dependency metadata in journal or a small table:

```text
ProcessStepBlockedDependency
- ProcessRunId
- StepRunId
- SourceStepRunId
- ArtifactExpectationId
- Status Requested/Satisfied/Failed
- Fingerprint
```

Then a runtime sweep can unblock exact dependents after artifact creation.

## Avoid Infinite Upstream Reruns

Use a materialization fingerprint:

```text
processRunId|downstreamStepRunId|sourceStepRunId|artifactExpectationId|sourceAttempt
```

If the same source step has already failed to materialize the same artifact after a fresh attempt, escalate to manager or human instead of looping.
