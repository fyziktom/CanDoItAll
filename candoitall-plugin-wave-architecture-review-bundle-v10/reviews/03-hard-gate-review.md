# Hard-gate review

## Why the phase9 hard gate was not enough
The old gate looked for:
- retired legacy carrier symbols,
- old normalization method names,
- a few known page/editor anti-patterns.

It did **not** verify the actual invariant:
- no persistence mutation in the active read seam.

## What phase10 changes
The phase10 gate now:
- inspects the `LoadAsync(...)` method body,
- recursively inspects local helper methods reachable from `LoadAsync(...)`,
- fails on direct/transitive persistence mutations,
- fails when the required proof tests are missing,
- warns on remaining compatibility fallbacks and hotspots.

## Expected behavior today
The phase10 gate is expected to fail on the current repo, because the current repo still contains the unresolved blocker.
