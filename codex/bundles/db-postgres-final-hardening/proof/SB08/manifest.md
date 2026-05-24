# SB08 proof manifest

## Status

Completed with documented broad-suite caveats.

## Changed files

| File | SHA-256 before | SHA-256 after | Reason |
|---|---|---|---|
| Implementation, tests, docs, and proof reports | See `transcripts/changed-file-hashes.txt` | See `transcripts/changed-file-hashes.txt` | Final hardening implementation and proof. |

## Commands

| Command | Result | Transcript |
|---|---|---|
| `dotnet build .\CanDoItAll.slnx -m:1 -v:minimal` | Passed with existing EF assembly-version warnings | `transcripts/solution-build.txt` |
| Full unit tests | Passed, 789 tests | `transcripts/unit-tests.txt` |
| Full component tests | Timed out at 30 minutes | `transcripts/component-tests.txt` |
| Component hang diagnostic | Failed by blame collector after 198 passed tests; active test captured | `transcripts/component-tests-hang-diagnostic.txt` |
| Full integration tests | Timed out at 20 minutes | `transcripts/integration-tests.txt` |
| Integration hang diagnostic | Timed out at 30 minutes after progress and two database transfer credential failures | `transcripts/integration-tests-hang-diagnostic.txt` |
| Single failing integration transfer test | Failed with PostgreSQL credential error for user `postgres` | `transcripts/integration-project-package-single-test.txt` |
| EF pending model check | Passed, no pending model changes | `transcripts/ef-pending-model-changes.txt` |
| Final residue search | Expected quarantine/test-only matches only | `transcripts/final-residue-rg.txt` |
| Bundle validator | Passed | `transcripts/bundle-validate.txt` |

## Source assertions

| Assertion | Source | Proof |
|---|---|---|
| The solution builds after hardening changes. | `CanDoItAll.slnx` | Solution build transcript. |
| EF model has no pending migration changes. | PostgreSQL migrations project and web startup | EF transcript. |
| SQLite/runtime drift is not reintroduced in production paths. | Final residue search | Residue transcript. |

## Negative tests

| Scenario | Expected | Result |
|---|---|---|
| Full unit suite regression. | Green. | Passed. |
| Full component suite hang. | Green or named caveat. | Named caveat: `CanDoItAll.Tests.Components.ProcessWorkspaceTests.Steps_canvas_node_moves_update_role_and_branch_positions_in_editor_state`. |
| Full integration suite blocker. | Green or named caveat. | Named caveat: database transfer tests still rely on auto-provisioned `postgres/postgres` current runtime in this environment. |

## Remaining risks

Broad component and integration suites are not fully green in this environment. Targeted hardening tests, full unit suite, solution build, EF model check, and residue audits passed. See `final-execution-report.md` for the merge recommendation.
