# SB01 startup baseline

## Method

The integration harness decorates the real `FileSandboxWorkspaceStore` and provider registry, replaces only the runtime and execution-event sink, and records strongly typed milestones using `Interlocked` sequence plus `Stopwatch` timestamps. Every row uses a fresh isolated test environment. Warm rows configure the existing preparation pool with capacity one, call `WarmAsync`, acquire the selected agent, and then reset all startup counters immediately before `SendMessageAsync`.

The diagnostic duration begins at the first execution-path catalog-load invocation and ends at runtime entry. It is not labeled acceptance-to-runtime because the current architecture has no pre-catalog acceptance event. The test gates behavior on ordering and operation counts, not milliseconds.

## Reproducible operation counts

| Scenario | Catalog loads | Provider gets | Session gets | Summary lists | Run-detail gets | Run-detail saves |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Cold/new session | 2 | 1 | 0 | 0 | 1 | 2 |
| Preparation-warm/new session | 2 | 1 | 0 | 0 | 1 | 2 |
| Cold/existing session | 2 | 1 | 2 | 1 | 1 | 2 |
| Preparation-warm/existing session | 2 | 1 | 2 | 1 | 1 | 2 |

All 12 cases across three iterations produced exactly these counts. Every case also proved:

`Planning persisted` → `ExecutionUpdated` → `IAgentExecutionEventSink` → `IAgentRuntime.RunAsync`

## Diagnostic timings

Milliseconds from first catalog-load invocation to runtime entry:

| Scenario | Iteration 1 | Iteration 2 | Iteration 3 | Median | Observed p95 |
| --- | ---: | ---: | ---: | ---: | ---: |
| Cold/new session | 258.902 | 269.882 | 261.325 | 261.325 | 269.882 |
| Preparation-warm/new session | 262.238 | 161.114 | 240.370 | 240.370 | 262.238 |
| Cold/existing session | 212.254 | 192.501 | 277.801 | 212.254 | 277.801 |
| Preparation-warm/existing session | 536.997 | 484.637 | 416.452 | 484.637 | 536.997 |

`Observed p95` uses nearest-rank selection over three diagnostic samples and is descriptive only. Environment bootstrap is excluded from the milestone duration but still dominates total test-case duration.

## Decision

The current preparation pool does not prepare execution. Warming it changes neither catalog, provider, session, summary, nor run-detail operation counts. Any apparent millisecond difference is noisy and cannot be claimed as a startup improvement.

The measurable optimization targets are therefore:

- remove or coalesce the second catalog load;
- remove or coalesce the second existing-session read and blocking summary query when the already-read state proves the same invariant;
- retain the real provider-registry fallback as a separately observable concern;
- introduce an immediate typed activity before the first catalog load so the current pre-runtime gap is truthful and measurable.

Raw evidence: `bundle://proof/SB01/transcripts/passing-startup-baseline-matrix.txt`.
