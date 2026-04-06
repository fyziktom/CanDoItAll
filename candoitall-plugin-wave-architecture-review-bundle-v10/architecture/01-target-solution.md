# Target solution

## Desired architecture end-state
The workbench structure model must have three clearly separated concerns:

1. **Canonical persisted truth**
   - canonical project nodes,
   - canonical user-authored links,
   - binding records,
   - reference rows,
   - explicit layout overrides.

2. **Read-time in-memory composition**
   - projection contributors build system-managed projection nodes/links in memory only,
   - marker/reference compatibility fallback may be read-only while migration remains incomplete,
   - no read path persists cleanup or normalization.

3. **Explicit repair / maintenance**
   - stale legacy projection artifacts can be retired,
   - orphan layout overrides can be cleaned up,
   - repair is idempotent and deliberate,
   - repair is not reachable from user reads.

## Key invariant
`GetStructureAsync` / `TryGetStructureAsync` / `LoadAsync` / `FindNodeAsync`
must be safe to call repeatedly and concurrently without changing persisted state.

## Why this is the right boundary for the next plugin wave
Future plugins will add more projection contributors, more manifest-driven editors, and more connector-driven operations. The base must be stable enough that:
- reads never “fix” persistence,
- plugin authors cannot accidentally rely on hidden side effects,
- compatibility cleanup has an explicit owner and proof.
