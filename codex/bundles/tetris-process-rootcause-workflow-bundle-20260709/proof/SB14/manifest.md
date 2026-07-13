# SB14 Proof Manifest

- Status: `Completed`
- Owned requirement: R14
- Semantic invariant contract: `bundle://proof/SB14/semantic-invariants.md`

## Required Artifacts

- `bundle://proof/SB14/changed-file-hashes.txt`
- `bundle://proof/SB14/transcripts/package-review.txt`
- `bundle://proof/SB14/transcripts/passing-tests.txt`
- `bundle://proof/SB14/transcripts/process-api.txt`
- `bundle://proof/SB14/transcripts/agent-api.txt`
- `bundle://proof/SB14/transcripts/source-assertions.txt`
- `bundle://proof/SB14/transcripts/anti-stub-audit.txt`
- `bundle://proof/SB14/transcripts/codeanalytics.txt`
- `bundle://proof/SB14/red-team-review.md`

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Process-bound agent execution evidence | `bundle://proof/SB14/transcripts/agent-api.txt` | `bundle://proof/SB14/transcripts/process-api.txt` | automated dispatcher history in the process transcript | transcript proves no manual transitions or detached test chat |
| Current-run tool receipts and artifacts | `bundle://proof/SB14/transcripts/agent-api.txt` | process step completion/readback in `bundle://proof/SB14/transcripts/process-api.txt` | same process/step/execution identifiers | red-team review rejects stale/copied receipts |

## Closure Evidence

- Autonomous root process run `4749e033-4326-4b58-acdf-61a5cf372563` completed at sequence `72674` with zero diagnostics and without selecting `repair-escalation`.
- All seven process runs in the hierarchy completed with zero diagnostics. Forty-two process-bound agent executions across seven agents completed through `OpenAI chat completions` on `gpt-5.4-mini`; there were zero pending approvals.
- QA routed a real scaffold/browser defect to repair. The mutation-capable repair step was required to provide current-run build, test, browser snapshot, screenshot, console, and stop receipts before completion. QA recheck then passed.
- Current-run browser snapshot and console evidence contain no Blazor fatal banner and zero console errors. The final screenshot was visually inspected.
- Project-structure readback preserved one workflow definition and one process definition and projected seven completed nodes for the final run, including runtime and screenshot nodes.
- `Microsoft.Extensions.AI.Abstractions` was aligned to `10.7.0`; the current Microsoft Agent Framework/OpenAI package versions were retained after compatibility review.
- The process-focused unit slice passed 701/701. The full unit run passed 1,982 tests and hit one known Windows `SUBST` race; that exact test passed 1/1 immediately when rerun alone. Four affected production projects built with zero warnings and zero errors.
- Final CodeAnalytics snapshot `snap-20260710022410-27d4d127` has no blocking errors and no dependency cycles.
