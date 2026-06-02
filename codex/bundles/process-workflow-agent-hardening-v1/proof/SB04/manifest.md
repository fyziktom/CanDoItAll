# SB04 Proof Manifest

## Status

Passed. SB04 hardened browser tool policy, structured browser proof validation, runtime host identity receipts, and cleanup/build-lock evidence.

## Delivered Changes

- Hardened `AgentToolInvocationPolicy` so all canonical browser tools require `CaptureRuntimeProof`.
- Added `ProcessBrowserProofValidator` with strongly typed proof, runtime host, viewport, tool output, and cleanup receipt records.
- Wired optional browser proof record validation into process dispatch completion checks.
- Extended `workspace_dotnet_run` startup receipts with host URL, database profile fields, cleanup receipt path, cleanup state, process ids, lifetime scope, and stop command.
- Added focused tests for allowed-operation to browser-tool mapping, stale/copied/wrong-host/wrong-profile/missing-cleanup proof rejection, runtime receipt fields, cleanup/build-lock script behavior, and existing dispatch browser proof behavior.

## Command Transcripts

- `proof/SB04/transcripts/tool-policy-tests.txt`
- `proof/SB04/transcripts/runtime-host-command-tests.txt`
- `proof/SB04/transcripts/runtime-host-command-tests-after-cleanup-assertions.txt`
- `proof/SB04/transcripts/browser-proof-validator-tests.txt`
- `proof/SB04/transcripts/process-dispatch-browser-proof-slice.txt`
- `proof/SB04/transcripts/failing-first-browser-tool-policy-mutation.txt`
- `proof/SB04/transcripts/browser-tool-policy-restored-tests.txt`
- `proof/SB04/transcripts/source-assertions.txt`
- `proof/SB04/transcripts/anti-stub-audit.txt`
- `proof/SB04/transcripts/prepared-validator-after-sb04.txt`
- `proof/SB04/transcripts/git-diff-check-after-sb04.txt`

## Shallow-Pass Trap

The policy tests do not merely assert that browser evidence tools are present. They assert that interaction browser tools (`browser_navigate`, `browser_click`, `browser_type`, and `browser_press_key`) are denied without `CaptureRuntimeProof` and allowed with it.

## Adversarial Negative Proof

`proof/SB04/transcripts/failing-first-browser-tool-policy-mutation.txt` temporarily weakened the browser proof tool set to screenshot/snapshot/console/evaluate only. The targeted tests failed because navigation, click, type, and press-key tools were incorrectly allowed without `CaptureRuntimeProof`. The mutation was reverted and `proof/SB04/transcripts/browser-tool-policy-restored-tests.txt` passed afterward.

The validator tests also reject adversarial proof records:

- Stale proof captured before execution start.
- Copied browser output not produced by the current execution.
- Wrong runtime host.
- Wrong database profile.
- Missing cleanup receipt for a kept-alive host.
- Evidence paths from another process run.

## Semantic Positive Proof

Passing targeted slices:

- `AgentToolInvocationPolicyTests`: 128 passed in the full policy slice; 8 passed in the restored SB04 browser policy slice.
- `WorkspaceCommandExecutionServiceTests`: 18 passed after cleanup/build-lock assertions.
- `ProcessBrowserProofValidatorTests`: 7 passed, covering positive proof and all required negative cases.
- Process-dispatch browser proof slice: 5 passed, confirming existing dispatch browser proof behavior remains intact.
- Prepared bundle validator: PASS after SB04.
- `git diff --check`: exited cleanly; transcript contains only Git CRLF normalization warnings.

## Source Assertions

`proof/SB04/transcripts/source-assertions.txt` confirms the production source contains catalog-backed browser policy, `CaptureRuntimeProof` operation enforcement, runtime receipt host URL and DB profile fields, cleanup receipt path, process-tree stopping, `ProcessBrowserProofValidator`, typed browser proof records, and dispatch validator wiring.

## Anti-Stub Audit

`proof/SB04/transcripts/anti-stub-audit.txt` found only pre-existing Tetris fixture strings in unrelated unit test payloads. No SB04 production file contains `TODO`, `NotImplementedException`, fixture-specific behavior, fake proof logic, or stubbed validation.

## Raw Note Literal Closure

- Browser proof policy mismatch: closed by catalog-backed browser tool policy and mutation-backed tests.
- Stale browser proof: closed by timestamp and current-run path validation.
- Copy-only proof: closed by successful-output/current-run artifact binding.
- Wrong host and port drift: closed by runtime host URI validation.
- Database profile drift: closed by database profile id/fingerprint validation.
- Runtime command semantics and build locks: closed by startup receipt identity fields, cleanup receipt path, process-tree stop behavior, and stop command for kept-alive runs.
- Repeated tool invocation guard/provider usage interaction: no SB04 production change was required; SB03 preserves provider usage in failure paths, and SB04 dispatch proof validation keeps browser evidence checks explicit.

## Additional Artifacts

- `proof/SB04/semantic-invariants.md`
- `proof/SB04/changed-file-hashes.md`
- `proof/SB04/production-behavior-artifact-matrix.md`
- `proof/SB04/browser-validation-analytics.md`
