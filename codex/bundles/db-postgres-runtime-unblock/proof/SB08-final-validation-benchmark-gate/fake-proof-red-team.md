# Fake-Proof Red Team

## Decision

Pass with two explicit validation blockers that must not be rewritten as success.

## Checks

| Check | Result | Evidence |
|---|---|---|
| Critical manifests exist | Pass | SB02, SB03, SB04, SB05, SB06, and SB08 each have `manifest.md` and `semantic-invariants.md`. |
| Proof paths are artifact-backed | Pass | Command transcripts, screenshots, source assertions, anti-stub audit, and hash inventory exist under `bundle://proof/`. |
| Source behavior is not fixture-only | Pass | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/source-assertions.txt` cites production files, not only tests. |
| Anti-stub audit | Pass | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/anti-stub-audit.txt` found no stub markers in changed production files. |
| UI proof is real browser proof | Pass | `bundle://proof/SB04-maintenance-restart-db-activation/transcripts/dotnet-test-playwright-database-switch.txt` plus three screenshots under `bundle://proof/SB04-maintenance-restart-db-activation/browser/`. |
| Remote branch currency | Blocked | `bundle://proof/SB01-rebase-scope-cleanup/transcripts/git-fetch-origin.txt` failed with SSH public key authentication. |
| Broad non-quarantined integration | Blocked | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-test-integration-nonquarantined.txt` timed out after local PostgreSQL default-user authentication failures. |

## Rejected Shallow Proof

- A final report that says "all tests passed" is false. Focused integration passed; broad non-quarantined integration is blocked by environment setup.
- A branch readiness claim based on local `development` only is incomplete. Local ancestry passed, but `origin/development` could not be fetched from this machine.
- A UI-only proof for restart-first activation is insufficient. Closure requires backend result fields, API/UI consumption, component tests, and Playwright state proof; those are all cited in SB04.

## Follow-Up Before Merge

Rerun `git fetch origin`, prove `origin/development` ancestry, and rerun the broad non-quarantined integration command with the expected PostgreSQL test credentials.
