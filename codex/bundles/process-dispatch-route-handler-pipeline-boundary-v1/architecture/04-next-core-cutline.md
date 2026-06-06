# Next Core Cutline

Do not start Process Core in this bundle.

A future Core extraction becomes safer only after:

- Route handler pipeline is module-local and stable.
- Candidate hydration is isolated behind stable snapshots.
- Claim model is no longer nested inside dispatcher or has a clear adapter.
- Transition/finalizer side effects are named and test-covered.
- Driver readiness concepts are documented but not implemented.
- Residual dispatcher files are small enough that ownership boundaries are visible.

Expected next post-bundle options:
1. Candidate hydration/direct-agent binding model split.
2. Transition/finalizer application boundary.
3. First minimal `Processes.Abstractions` planning bundle, still without moving runtime behavior.
