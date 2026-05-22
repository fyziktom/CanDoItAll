# SB02 Proof Manifest

## Status

- `Required during execution`

## Required Artifacts

| Artifact | Required path or rule | Status |
| --- | --- | --- |
| Failing-first missing screenshot transcript | `proof/SB02/evidence/failing-first-missing-screenshot.txt` | Pending |
| Failing-first shallow interaction transcript | `proof/SB02/evidence/failing-first-shallow-interaction.txt` | Pending |
| Passing runtime proof gate transcript | `proof/SB02/evidence/passing-runtime-proof-gate.txt` | Pending |
| Console phase transcript | `proof/SB02/evidence/console-phase-classification.txt` | Pending |
| Changed-file hashes | `proof/SB02/evidence/changed-file-hashes.txt` | Pending |
| Source assertions | `proof/SB02/evidence/source-assertions.txt` | Pending |
| Anti-stub audit | `proof/SB02/evidence/anti-stub-audit.txt` | Pending |

## Production Behavior Artifact Matrix

| Production artifact or signal | Producer | Consumer | Lifecycle proof required | Negative-test citation |
| --- | --- | --- | --- | --- |
| Missing/invalid browser proof conformance observation | Process proof validator | Process diagnostics and operator review | Emitted before QA/release acceptance | Missing managed screenshot cannot complete quality accepted |
| Console phase classification | Console proof validator | QA outcome and evidence pack validation | Active proof window evaluated before cleanup | Active JS error blocks; post-stop disconnect classified |
| Representative interaction assertion | QA proof validator from project structure or step contract | Process step outcome | Evaluated after browser interaction and before acceptance | Pause/page-load-only proof fails when step requires interactive behavior |

## Completion Rule

This manifest is complete only when failing-first and passing transcripts prove behavior through production validation paths.
