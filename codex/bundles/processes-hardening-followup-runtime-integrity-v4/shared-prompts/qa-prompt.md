# Shared QA Prompt

Audit the implementation for these failure modes:

- Does the test use the production path or manually seed the final state?
- Can a non-mutating step still mutate product targets through a script or helper?
- Can stale free text promote an alias to writable?
- Can a newly recorded upstream artifact actually unblock the dependent step?
- Can artifact lineage survive key truncation?
- Does the finalizer validate actual stored content?
- Does a negative branch outcome hide missing own required artifacts?
- Does no-progress detection survive process/runtime restart?
- Are all new process semantics generic across process domains?
