# SB05 Proof Manifest

## Changed Files

No production source files were changed in SB05. The work used application APIs and stored proof under the requested output root.

| File | SHA256 |
| --- | --- |
| `bundle://proof/SB05/backups/backup-summary.json` | `BD6554122C978A4977B04B9679FD2F02FABD9006FBE33F061C0DB35591717DA0` |

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Project-structure backup summary | Project-structure API export workflow | Demo rerun operator | Stored under `bundle://proof/SB05/backups/backup-summary.json` and local output root for rerun recovery | API-only backup transcript in `bundle://proof/SB05/transcripts/api-backup-and-seed.txt` |
| Current Blazor process definitions | Process template import API | Process launch UI/API | Latest reusable Blazor templates exist in PostgreSQL; older zero-run duplicates were removed through API | Definition list in `bundle://proof/SB05/transcripts/api-backup-and-seed.txt` |

## Validation

- `bundle://proof/SB05/transcripts/api-backup-and-seed.txt`
- `bundle://proof/SB05/backups/backup-summary.json`
- `bundle://proof/SB05/transcripts/anti-stub-audit.txt`
- Semantic invariant contract: `bundle://proof/SB05/semantic-invariants.md`
- Failing-first transcript: N/A process-data backup/import proof; API-only behavior is validated by the API transcript and backup artifact.
- Passing transcript: `bundle://proof/SB05/transcripts/api-backup-and-seed.txt`
- Anti-stub audit transcript: `bundle://proof/SB05/transcripts/anti-stub-audit.txt`
- Runtime output root: `C:\programovani\dotnet-demo\output\codex-live-blazor-20260522-192839` (non-artifact local context requested by user).

## Closure

SB05 is complete. Data changes were made through APIs only; no direct PostgreSQL table edits or test-seeded project-structure data were used.
