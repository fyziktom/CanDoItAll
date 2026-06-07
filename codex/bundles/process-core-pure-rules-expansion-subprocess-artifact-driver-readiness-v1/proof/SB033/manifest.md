# SB033 Proof Manifest
## Summary
- Subbundle: SB033 - Gate K broad proof closure.
- Status: Completed.
- Invariant ID: SB033-INV-001
- Hash reference: repo://codex/bundles/process-core-pure-rules-expansion-subprocess-artifact-driver-readiness-v1/reviews/02-final-red-team-review.md SHA-256 e08c1bf342dbe42f9e7afc7c8399e34f7eecb8f55400c514b4143616f0e7f914
- Semantic invariant contract: bundle://proof/SB033/semantic-invariants.md
- Changed file: repo://codex/bundles/process-core-pure-rules-expansion-subprocess-artifact-driver-readiness-v1/reviews/02-final-red-team-review.md
## Evidence
- Source assertion transcript: bundle://proof/shared/transcripts/source-assertions.txt
- Source proof artifact: bundle://reviews/02-final-red-team-review.md
- Passing transcript: bundle://proof/shared/transcripts/unit-full.txt
- Failing-first transcript: bundle://proof/SB033/transcripts/failing-first.txt
- Anti-stub audit transcript: bundle://proof/shared/transcripts/anti-stub-scan.txt
- Dependency scan transcript: bundle://proof/shared/transcripts/core-forbidden-scan.txt
- Build transcript: bundle://proof/shared/transcripts/build.txt
- Driver token scan transcript: bundle://proof/shared/transcripts/driver-token-scan.txt
- No UI/media transcript: bundle://proof/shared/transcripts/no-ui-media-drift-scan.txt
## Closure
- Expected behavior: Build, full unit, focused integration, no-UI/media, and anti-stub scans all pass before final report closure.
- Disallowed shallow implementation: Counting written summaries as proof without command transcripts.
- Downstream dependency check: bundle://proof/shared/transcripts/no-ui-media-drift-scan.txt
