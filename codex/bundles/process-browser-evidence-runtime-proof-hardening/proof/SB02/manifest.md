# SB02 Proof Manifest

## Status

- `Completed`

## Required Artifacts

| Artifact | Required path or rule | Status |
| --- | --- | --- |
| Missing screenshot negative fixture | `ProcessRunAutomationDispatchServiceTests.ResolveCompletionStatus_blocks_completed_qa_when_required_browser_screenshot_artifact_is_missing` | Passed in `bundle://proof/SB02/evidence/passing-runtime-proof-gate.txt` |
| Shallow interaction negative fixture | `ProcessRunAutomationDispatchServiceTests.ResolveCompletionStatus_blocks_interactive_browser_proof_without_representative_interaction_tool` | Passed in transcript |
| Passing runtime proof gate transcript | `bundle://proof/SB02/evidence/passing-runtime-proof-gate.txt` | Passed, 8 targeted tests |
| Console phase transcript | `bundle://proof/SB02/evidence/passing-runtime-proof-gate.txt` | Active JS error blocks; post-stop disconnect classified |
| Changed-file hashes | `bundle://proof/SB02/evidence/changed-file-hashes.txt` | Captured |
| Source assertions | `bundle://proof/SB02/evidence/source-assertions.txt` | Captured |

## Production Behavior Artifact Matrix

| Production artifact or signal | Producer | Consumer | Lifecycle proof required | Negative-test citation |
| --- | --- | --- | --- | --- |
| Missing/invalid browser proof conformance observation | Process proof validator | Process diagnostics and operator review | Emitted before QA/release acceptance | Missing managed screenshot cannot complete quality accepted |
| Console phase classification | Console proof validator | QA outcome and evidence pack validation | Active proof window evaluated before cleanup | Active JS error blocks; post-stop disconnect classified |
| Representative interaction assertion | QA proof validator from project structure or step contract | Process step outcome | Evaluated after browser interaction and before acceptance | Pause/page-load-only proof fails when step requires interactive behavior |

## Completion Rule

This manifest is complete for code-level closure: production validation rejects missing, invalid, shallow, or console-broken browser proof and allows classified post-stop disconnects only after usable browser evidence exists.
