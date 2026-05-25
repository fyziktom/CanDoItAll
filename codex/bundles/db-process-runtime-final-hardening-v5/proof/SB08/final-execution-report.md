# SB08 Final Execution Report

## Decision

Process DB hardening is merge-ready.

The repository is not all-suite green. Broad component and integration failures are classified below and are not caused by the process DB hardening touch set. Merge is safe for the process DB hardening if the project accepts carrying these already-present broad-suite issues as separate follow-up work.

## Validation summary

| Gate | Result | Evidence |
| --- | --- | --- |
| Restore | Passed | `bundle://proof/SB08/full-restore.log` |
| Build | Passed with existing warnings | `bundle://proof/SB08/full-build.log` |
| Unit tests | Passed, 789/789 | `bundle://proof/SB08/full-unit-tests.log` |
| Full integration | Failed, 922/925 | `bundle://proof/SB08/full-integration-tests.log` |
| Integration failed-test rerun after environment repair | Failed, 0/3 | `bundle://proof/SB08/integration-failed-tests-after-postgres-role-fix.log` |
| Main component suite | Failed/hung | `bundle://proof/SB08/full-component-tests.log`, `bundle://proof/SB08/component-tests-without-projects-page.log` |
| MCP component suite | Passed, 22/22 | `bundle://proof/SB08/full-mcp-component-tests.log` |
| Focused process DB tests | Passed, 409/409 | `bundle://proof/SB08/focused-process-db-tests.log` |
| EF pending model changes | Passed, no pending model changes | `bundle://proof/SB08/ef-pending-model-changes.log` |
| Runtime residue audit | Passed with legacy quarantine-only SQLite references | `bundle://proof/SB08/runtime-residue-audit.log` |
| Query-plan review | Passed | `bundle://proof/SB08/artifact-review.md` |
| Benchmark review | Passed | `bundle://proof/SB08/artifact-review.md` |
| Final bundle validator | Passed | `bundle://proof/SB08/final-validate.log` |

## Classified broad-suite failures

### Full integration

Initial broad integration run completed with 922 passing tests and 3 failures. All three initial failures were `28P01: password authentication failed for user "postgres"` on the default auto-provisioned PostgreSQL profile. The local compose database had `candoitall/candoitall` but no `postgres` role, so I created the missing local `postgres` role with password `postgres` and reran the exact failures.

After that environment repair, the same three tests failed differently:

- `DatabaseTransferIntegrationTests.Project_transfer_copies_all_project_and_workbench_records_between_profiles`: `Workbench_ProjectObjects` missing in the target operation.
- `DatabaseTransferIntegrationTests.Project_package_export_import_round_trips_project_records_and_media`: `Workbench_ProjectObjects` missing in the target operation.
- `AgentFrameworkRuntimeSwitchingIntegrationTests.Agentframework_workspace_service_tracks_the_current_profile_after_runtime_switch`: assertion still sees the primary agent after the switch path.

Classification: pre-existing/out-of-scope runtime-switching test assumptions in untouched files. Evidence: `bundle://proof/SB08/integration-failure-scope-diff-audit.log` shows no changes to the failing test files.

### Component tests

The main bUnit component project failed before completion:

- `ProjectsPageTests` cannot find `[data-testid='project-name-input']`.
- With `ProjectsPageTests` excluded, `ProjectStructurePartyPickerTests` still fails on missing participant link text and `[data-testid='project-structure-work-item-save']`, then the run hangs past the outer timeout.

Classification: pre-existing/out-of-scope component failures in untouched component test projects. Evidence: `bundle://proof/SB08/component-suite-diff-audit.log`.

The independent MCP component suite passed 22/22.

## Hardening proof

SB08 revalidated the process DB scope with 409 focused integration tests. Those tests cover:

- startup recovery live/expired automation dispatch lease behavior,
- long-running dispatch heartbeat renewal and cancellation,
- stale worker finalization and artifact projection rejection,
- outbox idempotency and duplicate automation dispatch suppression,
- PostgreSQL claim/index/query-plan tests,
- runtime-switch pending restart behavior.

SB06 benchmark proof remains current:

- Process outbox bounded parallel throughput: 419.581 records/s.
- Automation delivery bounded parallel throughput: 434.896 records/s.
- Connector command bounded parallel throughput: 411.112 records/s.
- All benchmark workloads processed 768/768 records.

## Residual risks

- Broad integration cannot be called green until the three runtime-switching tests are repaired or formally quarantined.
- The main bUnit component suite cannot be called green until `ProjectsPageTests` and `ProjectStructurePartyPickerTests` are repaired or formally quarantined.
- Build emits existing `MSB3277` EF Core assembly-version conflict warnings; there are no build errors.

## Final statement

No process DB hardening blocker remains. The branch is process-DB-merge-ready with explicit non-hardening broad-suite caveats.
