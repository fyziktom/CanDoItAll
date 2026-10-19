# P04: avoid repeated validation of an existing directory tree

Prepared before source edits, 2026-10-19. The selected publication lease is correct under
the current functional tests, but the one-second unrelated-writer criterion still fails.

## Evidence and decision

The completed `c4-selected-profile-v3` trace has zero reported lost events and complete
method rundown. During the 1.555-second publication/writer window, path traversal has
2,298 sampled thread-milliseconds and `EnsureDirectoryTree` has 1,005. These are nested,
concurrent thread samples, not additive wall time, CPU percentages or disk measurements.
The preceding trace was truncated on test-host exit and is not used for attribution.

`EnsureDirectoryTree` first creates a fresh path policy and resolves the full target,
which checks containment and every ancestor. It then walks every segment and checks all
ancestors again before and after each segment, even when the directory already exists
and no permissions need changing. This repeats a prefix walk quadratically with depth.

Add one early return after the complete initial validation when all of these hold:

- private Unix permissions were not requested;
- the target uses exact ordinal containment and case sensitivity is known;
- the fully validated target is an existing directory.

The method then has no directory mutation to perform. Missing paths, unknown case facts,
case-variant paths and private-directory hardening retain their existing path. Each call
creates a new policy; no cached authorization or filesystem metadata crosses a call,
await, callback or lock wait. The writer still performs its existing fresh validation
after directory preparation, coordination acquisition and the pre-commit callback.
Actual payload flushes, coordination, pending bytes and atomic replacement are unchanged.

## Boundary, risk and validation

One private Infrastructure method changes, without a new interface, reference, cache or
application dependency. The risk is accidentally treating permission hardening or a
missing/case-ambiguous tree as a no-op; the explicit guard excludes each. Initial path
resolution must precede the return so an existing symlink target cannot bypass checks.

Run the 33 physical-path/durable-writer cases, including initial link refusal, parent/root
replacement at commit, missing paths, unknown/case-variant facts, permissions and a real
crashed writer. Run the 38 catalog/publication authority cases and the unchanged forced
SQL contention row. Refresh standard Release outputs and run complete portability-static,
rendering and documentation gates. A passing small cohort does not establish p95,
maximum-size capacity or fairness; those remain separate obligations.
