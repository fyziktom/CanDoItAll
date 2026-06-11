# SB06 Semantic Invariants

## Deterministic Release Matrix Is Green

- Invariant ID: `SB06_INV_001`
- Source raw note: determine whether processes already work like before before further Process Core extraction.
- Expected behavior: build, full unit rerun, focused integration matrix, and large-desktop Playwright launch-to-completed-run proof are all green before the release decision is made.
- Disallowed shallow implementation: claiming stabilization from one representative backend test, from run-created-only UI proof, or from a stale prior bundle result.
- Passing proof: `bundle://proof/SB06/transcripts/build.txt`, `bundle://proof/SB06/transcripts/unit-tests-rerun.txt`, `bundle://proof/SB06/transcripts/focused-integration-matrix.txt`, and `bundle://proof/SB06/transcripts/focused-playwright-final.txt`.
- Source assertions: `bundle://proof/SB06/transcripts/source-assertions.txt` verifies the result counts and required test names.

## Live OpenAI Smoke Is Explicitly Classified

- Invariant ID: `SB06_INV_002`
- Source raw note: classify live OpenAI template smoke honestly and run it only when explicit environment controls are present.
- Expected behavior: live OpenAI proof is run only when the API key and explicit opt-in/model/timeout/token-budget variables are present. Otherwise it is skipped and not counted as deterministic release proof.
- Disallowed shallow implementation: treating an available API key alone as enough live proof, leaking secret values, or counting a skipped live run as a pass.
- Passing proof: `bundle://proof/SB06/transcripts/live-openai-classification.txt` records the redacted API-key presence and absent explicit opt-in variables; `bundle://proof/SB06/transcripts/live-openai-settings-tests.txt` records 7 live settings guard tests passing.
- Negative proof: `bundle://proof/SB06/transcripts/red-team-verifier.txt` rejects counting live OpenAI proof while the opt-in variables are absent.

## Code-First Ratio Controls Merge Readiness

- Invariant ID: `SB06_INV_003`
- Source raw note: do not hide the previous code-first ratio blocker or count documentation/proof churn as implementation.
- Expected behavior: final release closure uses the explicit bundle-start SHA and current worktree to calculate source/test versus bundle churn. If the ratio fails, the release decision cannot be merge-ready.
- Disallowed shallow implementation: falling back to `HEAD`, ignoring untracked proof files, excluding bundle proof churn without a policy change, or adding non-functional code only to game the ratio.
- Passing proof: `bundle://proof/SB06/transcripts/code-first-ratio.txt` reports `RatioPass: False` with the explicit line counts.
- Negative proof: `bundle://proof/SB06/transcripts/red-team-verifier.txt` rejects merge-ready closure while the ratio transcript reports `RatioPass: False`.

## Raw Notes Are Closed Note By Note

- Invariant ID: `SB06_INV_004`
- Source raw note: review real code/tests, decide whether processes work like before, identify gaps, and prioritize stabilization before extraction.
- Expected behavior: raw-note closure maps each note to SB01-SB06 proof, including partial or negative results when applicable.
- Disallowed shallow implementation: summarizing residual risk without a note-by-note result or hiding a failed release gate behind green runtime tests.
- Passing proof: `bundle://reviews/01-execution-report.md` contains note-by-note closure and browser analytics; `bundle://reviews/02-release-decision.md` records the final `not merge-ready` decision.
- Negative proof: `bundle://proof/SB06/transcripts/red-team-verifier.txt` rejects claims that the bundle is merge-ready or live-validated when the ratio and live classification transcripts say otherwise.

## Production Behavior Artifact Matrix

| Signal or record | Producer | Consumer | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Deterministic runtime matrix | Build, unit, integration, and Playwright commands | Release decision | SB06 transcripts prove build success, full unit rerun success, focused runtime matrix success, and user-visible launch-to-completed-run success. | Source assertions fail if required transcript counts or test names are missing. |
| Live OpenAI classification | Environment-variable scan and live settings tests | Release decision | Classification transcript proves live smoke is skipped because explicit opt-in/model/timeout/token-budget variables are absent; settings tests prove guard behavior. | Red-team verifier rejects treating the skipped live smoke as proof. |
| Code-first ratio decision | Explicit-start diff and worktree scan | Release decision | Ratio transcript proves source/test line count, bundle line count, required 5x threshold, and failed ratio. | Red-team verifier rejects merge-ready decision while `RatioPass: False`. |
| Raw-note closure | Execution report and release-decision review | Bundle final validator and user handoff | Execution report maps each raw note to proof; release decision records deterministic green proof plus failed merge gate. | Completed-stage validator and source assertions fail if final closure artifacts are missing. |
