# Ordinary conversation transaction target

## Canonical state machine

```text
Idle(revision N)
  -> Admit turn:
       append user
       set ActiveTurn
       optionally adopt provider/model
       optionally clear incompatible acceleration
       persist revision N+1
  -> Invoke provider
  -> Complete:
       append assistant + usage
       clear ActiveTurn
       persist revision N+2

Failure after admission:
  -> remove the admitted user entry
  -> restore the pre-turn provider snapshot
  -> restore the pre-turn acceleration state
  -> clear ActiveTurn
  -> persist a new revision
```

## Concurrent operations

- A second turn is rejected while `ActiveTurn` exists.
- Rename is rejected while `ActiveTurn` exists in this follow-up. Do not implement merge-on-completion.
- Delete may remain explicit terminal removal, but its behavior during an active turn must be tested and
  documented.
- Store CAS must work across multiple scoped store instances pointing at the same root.
- The turn must reserve capacity for both the user and assistant entries before calling the provider.

## Persistence locking

Instance-local locks are insufficient. The accepted implementation must serialize the read-CAS-write
sequence by canonical document identity across every store instance in the process. A lock-file
implementation may additionally support multiple processes, but do not claim cross-process safety unless
it is tested.

Atomic temp-file writes must remove abandoned temp files on failure or cancellation.
