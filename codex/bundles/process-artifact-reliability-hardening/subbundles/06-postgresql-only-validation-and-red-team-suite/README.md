# SB06 - PostgreSQL-Only Validation And Red-Team Suite

## Status

Ready

## Objective

Run focused and broad validation for the hardened Processes runtime, keeping validation PostgreSQL-only and adding red-team tests for artifact/recovery edge cases.

## Covered Inputs

- N003, N006, N007
- All requirements PR-001 through PR-012

## Prerequisites

- SB01-SB05 complete and proof manifests available.

## Exact Source References

- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/`
- `repo://CanDoItAll.slnx`
- PostgreSQL migration project/files currently present in the repo, if data model changes are made.

## Deliverables

- Focused integration test suite for process artifact finalization.
- Red-team scenarios for wrong format, stale artifact, placeholder, response-text-as-evidence, wrong manager, workflow-backed completion, and repeated retries.
- PostgreSQL migration/model validation if schema changed.
- Final build/test proof and changed-file hashes.
- Final execution report closure.

## Dependency Impact

Final closure. This subbundle does not introduce new architecture unless validation reveals a blocker that requires reopening an earlier subbundle.

## Validation Depth

Full closure proof. Prefer focused tests first, then build/full relevant test suite.

## Implementation Steps

1. Run targeted tests for `ProcessRunAutomationDispatchServiceTests`.
2. Run additional new tests from SB01-SB05.
3. Run PostgreSQL migration/model validation if data model changed.
4. Run full solution build.
5. Run bundle validators at completed stage.
6. Audit for SQLite residue.
7. Update execution report, raw-note closure, and proof manifests.

## Scope Exceptions

- If the local PostgreSQL database is unavailable, record an explicit blocker with exact command output and run all non-database tests that can run.

## Do Not Do

- Do not add SQLite tests or migrations.
- Do not close with only unit tests if integration paths changed.
- Do not ignore flaky/hanging tests without recording diagnostics.

## Acceptance Checklist

- [ ] Focused process integration tests pass.
- [ ] Workflow-backed role finalizer test passes.
- [ ] Recovery red-team tests pass.
- [ ] Retry-loop tests pass.
- [ ] PostgreSQL validation is recorded if schema changed.
- [ ] Full solution build passes or blocker is explicit.
- [ ] No SQLite residue introduced.

## Proof Required

- `proof/SB06/manifest.md`
- `proof/SB06/semantic-invariants.md`
- test transcripts
- PostgreSQL validation transcript if applicable
- build transcript
- SQLite residue audit transcript
- changed-file hashes

## Browser Validation Logging

N/A unless new UI is added. If process run detail UI is changed to expose diagnostics, use Playwright and record browser analytics in `reviews/01-execution-report.md`.

## Progression Gate

Final closure gate. Do not mark the bundle complete until raw notes N001-N007 are closed or explicitly marked partial with blockers.

## Suggested Agent Prompt

Use the shared QA prompt at `bundle://shared-prompts/qa-prompt.md`, then run this final closure subbundle.
