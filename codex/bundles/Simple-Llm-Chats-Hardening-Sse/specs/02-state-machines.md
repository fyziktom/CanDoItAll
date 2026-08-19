# State machines

## Operation

| From | Action | To | Required durable checks |
|---|---|---|---|
| none | admit | Accepted/Queued | ID/fingerprint uniqueness, eligible conversation |
| Queued | claim | Claimed | CAS, owner/epoch, profile generation |
| Claimed | admit turn | Admitted/Running | no active turn, cancellation baseline |
| Running | first delta | Streaming | claim valid, bounds valid |
| Running/Streaming | complete | Succeeded | claim/profile valid, no winning cancellation, assistant commit |
| nonterminal | request cancel | CancellationRequested | monotonic cancellation generation |
| nonterminal | cancelled finalize | Cancelled | cancellation wins, active turn cleared |
| nonterminal | provider fail | Failed | compensation complete |
| nonterminal | uncertain/failed compensation | RecoveryRequired | exact unresolved evidence persisted |
| terminal | replay same fingerprint | same terminal | no provider dispatch |
| any existing | replay different fingerprint | Conflict | no mutation/dispatch |

## Execution lease

```text
Unclaimed -> Claimed(epoch N) -> Heartbeating -> Released/Terminal
                     |
                     +-> Expired -> Reconcile
                                      | no dispatch evidence -> requeue/claim N+1
                                      | possible dispatch -> RecoveryRequired
```

A stale epoch cannot append events or finalize.

## Stream

```text
NoDelta -> DeltasEmitted -> ProviderCompleted -> CanonicalFinalized -> TerminalEvent
   |            |
   + failure    + failure (no retry)
```

Retry is allowed only from `NoDelta` and only within configured attempt policy.

## Cancellation ordering

Use a durable generation or timestamp/version captured at admission. Success finalization must compare
the latest cancellation evidence in the same transaction. A cancellation committed before finalization
wins. A cancel arriving after immutable success returns the terminal result and does not rewrite history.
