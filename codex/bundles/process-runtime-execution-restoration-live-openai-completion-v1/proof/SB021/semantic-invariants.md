# SB021 Semantic Invariants

## Status
Completed.

## Invariant SB021_INV_001
- Invariant ID: `SB021_INV_001`
- Source raw note: live OpenAI proof is opt-in and may be skipped only with explicit reason.
- Expected behavior: The live OpenAI smoke runs only when opt-in, credentials, budget, and timeout are configured; otherwise the gate records an explicit skip that is not counted as live-provider functionality passing.
- Disallowed shallow implementation: Running a live API call without opt-in, logging secret values, or treating deterministic fake-provider tests as live OpenAI proof.
- Failing-first test: `bundle://proof/SB021/red-team/deterministic-tests-not-live-proof.txt` rejects deterministic-as-live proof.
- Passing proof: `bundle://proof/SB021/transcripts/live-openai-gate-decision.txt` records opt-in absent, key present without value, and budget/timeout absent.
- Changed source files: No production source changed in SB021. Current policy/template hashes are captured in `bundle://proof/SB021/manifest.md`.
- Production assertions: `bundle://proof/SB021/transcripts/live-openai-gate-source-assertions.txt`
- Red-team negative case: `bundle://proof/SB021/red-team/deterministic-tests-not-live-proof.txt`
- Downstream dependency check: SB022 may start with live smoke explicitly skipped; release notes must preserve that limitation.

## Shallow-Pass Trap
A fake Gate G closure could say "OpenAI passed" because deterministic provider tests passed or because an API key exists. SB021 rejects that: opt-in is absent and budget/timeout are absent, so the only valid outcome is an explicit policy skip.

## Semantic Positive Proof
- `bundle://proof/SB021/transcripts/live-openai-gate-decision.txt`
- `bundle://proof/SB020/openai-live-smoke-proof.md`

## Adversarial Negative Proof
- `bundle://proof/SB021/red-team/deterministic-tests-not-live-proof.txt`

## Anti-Stub Audit
- `bundle://proof/SB021/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Matches are documentation and negative test assertions, not an execution-capable process-driver runtime host, process-driver registry, selector, or production `NotImplemented` path.

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Live smoke skip | Configuration transcript | Execution report and release notes | Records explicit skip because opt-in and budget/timeout are absent | Deterministic tests are rejected as live proof |
| Secret redaction evidence | Configuration transcript | Security review | Only presence/absence is printed | API key value is never logged |
