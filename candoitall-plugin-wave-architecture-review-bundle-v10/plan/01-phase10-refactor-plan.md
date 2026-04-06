# Phase10 stabilization plan

## Phase 1 — close the read-path blocker
- remove every direct and transitive persistence mutation from `ProjectStructureAssemblyService.LoadAsync(...)`,
- keep any normalization strictly in-memory,
- make the active structure-read path zero-write.

## Phase 2 — move stale projection cleanup into an explicit repair seam
- create or reuse a dedicated maintenance boundary,
- move stale system-managed row retirement there,
- move orphan layout cleanup there,
- make the repair idempotent and independently testable.

## Phase 3 — add proof that bundle9 was missing
- add integration tests that seed stale system-managed nodes/links,
- add integration tests that seed stale layout overrides,
- add zero-write legacy-fallback proof,
- add repair-seam tests proving cleanup still works when explicitly invoked.

## Phase 4 — harden the gate
- replace narrow symbol-only closure checks with phase10 behavior checks,
- fail on direct or transitive write helpers reachable from `LoadAsync(...)`,
- fail when required proof tests are missing.

## Phase 5 — harden manifest-driven editor proof
- add unknown-provider and unknown-resource test plugins,
- exercise all shared editor field types,
- prove save/load round-trip without page-specific code changes.
