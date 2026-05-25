# SB01 proof manifest

## Status

Completed.

## Changed files

| File | SHA-256 before | SHA-256 after | Reason |
|---|---|---|---|
| `codex/bundles/db-postgres-final-hardening/scripts/audit_residue_and_bottlenecks.ps1` | See `../SB08/transcripts/changed-file-hashes.txt` | See `../SB08/transcripts/changed-file-hashes.txt` | Treat `rg` no-match exit code as successful residue proof. |
| `codex/bundles/db-postgres-final-hardening/scripts/validate_bundle.py` | new | See `../SB08/transcripts/changed-file-hashes.txt` | Restore local bundle validation gate. |

## Commands

| Command | Result | Transcript |
|---|---|---|
| `python codex/bundles/db-postgres-final-hardening/scripts/validate_bundle.py` | Passed | `../SB08/transcripts/bundle-validate.txt` |
| `powershell -NoProfile -ExecutionPolicy Bypass -File codex/bundles/db-postgres-final-hardening/scripts/audit_residue_and_bottlenecks.ps1` | Passed | `transcripts/residue-and-bottleneck-audit.txt` |
| `git fetch origin` | Failed, SSH key unavailable | `../SB08/transcripts/git-fetch.txt` |
| `git merge-base --is-ancestor origin/development HEAD` | Passed against local ref | `../SB08/transcripts/git-merge-base.txt` |

## Source assertions

| Assertion | Source | Proof |
|---|---|---|
| Residue audit no longer fails on legitimate no-match searches. | Audit script | `transcripts/residue-and-bottleneck-audit.txt` |
| Local `origin/development` is an ancestor of the working HEAD. | Git merge-base | `../SB08/transcripts/git-merge-base.txt` |

## Negative tests

| Scenario | Expected | Result |
|---|---|---|
| Runtime residue search for removed SQLite/hot-switch/drain/fake-proof patterns. | No unexpected matches. | Passed in residue audit transcript. |

## Remaining risks

Remote currency could not be refreshed because `git fetch origin` failed with `Permission denied (publickey)`. Merge-base proof is against the already present local `origin/development` ref.
