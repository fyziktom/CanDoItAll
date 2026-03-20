# Repair Plan

## Phase 1: Safety and contract blockers

Priority: release-blocking

1. Repair stale-process cleanup so it validates ownership before kill.
2. Repair stop semantics with graceful-then-force behavior and platform-aware terminators.
3. Repair `app_start(waitFor=...)` so wait failures are surfaced to clients.
4. Either implement `ContinueIfSafe` or remove it from the server contract and CodexPack.

Exit criteria:

- No public policy or behavior is advertised without implementation.
- Cleanup can no longer terminate unrelated processes.
- Start/wait behavior is honest and actionable.

## Phase 2: Public contract parity

Priority: high

1. Repair `workspace_info` so `includeHistory` is real or removed.
2. Add relative-path fields if they remain part of the accepted contract.
3. Redact configuration snapshots instead of returning raw overlays.
4. Complete wait semantics: `Ready`, stable health, correct quiet semantics, optional restart-complete support.
5. Repair build/test result models so build does not report a test runner.
6. Implement missing `tests_run` behavior: request overlay, runner detection, artifact reporting.

Exit criteria:

- The CodexPack contract matches the real server surface and payloads.

## Phase 3: Validation completion

Priority: high

1. Add missing P0 unit/integration tests from the validation matrix.
2. Add small fixtures where the current repo is too heavy for deterministic tests.
3. Make stdout cleanliness and stale cleanup tests mandatory in CI.
4. Add Windows and Linux coverage at minimum for process termination behavior.

Exit criteria:

- All P0 matrix items are automated and green.
- Integration coverage proves the repaired behavior, not only the happy path.

## Phase 4: Documentation sync

Priority: medium

1. Reconcile the CodexPack with the repaired implementation.
2. Remove any remaining over-promises from docs and prompts.
3. Mark unsupported follow-ups clearly as post-MVP rather than silently exposed behavior.

Exit criteria:

- Docs, prompts, and server behavior describe the same system.
