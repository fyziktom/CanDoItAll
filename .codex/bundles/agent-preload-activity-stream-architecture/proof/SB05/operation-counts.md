# SB05 Startup Operation Counts

## Before and after

| Operation | SB01 new | SB01 existing | SB05 all scenarios | Result |
| --- | ---: | ---: | ---: | --- |
| Typed accepted publication | 0 | 0 | 1 | New immediate feedback boundary |
| Catalog load | 2 | 2 | 0 | Removed from dispatch path |
| Immutable catalog snapshot read | 0 | 0 | 1 | One coherent in-memory read |
| Provider registry get | 1 | 1 | 0 | Removed from dispatch path |
| Provider snapshot acquisition | 0 | 0 | 1 | One immutable lease |
| Chat-session get | 0 | 2 | 0 | Duplicate existing-session reads removed |
| Run-summary list | 0 | 1 | 0 | Startup scan removed |
| Atomic chat-backed start | 0 | 0 | 1 | One durable transaction boundary |
| Run-detail get | 1 | 1 | 0 | Read-before-write removed |
| Run-detail save | 2 | 2 | 0 | Replaced by atomic start |
| Run-detail update | Not separately counted | Not separately counted | 1 | Runtime transition persisted once |

The three provider snapshot captures are separate O(1) validation/fencing reads at
execution boundaries. They do not open a database context or reload a provider.

## File-store scaling proof

`FileSandboxWorkspaceAdmissionReadScalingIntegrationTests` passed 6/6 against both
4 and 96 historical runs:

- new-session admission remains 11 physical JSON reads;
- existing-session terminal-latest admission remains 15 physical JSON reads;
- active latest-run lookup uses the state-only path;
- read counts do not scale with historical run count.

The chat index is O(1) in record lookup count. Its current JSON representation still
has O(R) bytes/parse CPU as the index grows; this is an explicit residual storage
format cost, not a hidden constant-time claim.

## Recovery cost boundary

The normal path does not perform a read-before-write solely for recovery. Typed WAL
records are written around the multi-file boundary. Recovery preflights persisted
state, detects conflicts, and then rolls forward idempotently.

- generic-new recovery matrix: 6/6;
- combined chat-start, existing-update, and generic journal regression: 33/33;
- usage projection delta work is O(A + P + M) for affected agent/provider/model
  aggregates, not universally O(1).
