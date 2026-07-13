# Semantic Invariants - SB09

## INV-SB09-01

- Invariant ID: `INV-SB09-01`
- Source raw note: B07 requires regression harness and architecture closure for the full failure class.
- Expected behavior: final closure includes focused regressions, full process-filter coverage, full unit-suite coverage, changed-file hashes, anti-stub audit, and explicit live-instance blocker status.
- Disallowed shallow implementation: closing with only source edits and no repeatable tests or proof artifacts.
- Failing-first test: `bundle://proof/SB09/transcripts/adversarial-negative.md`
- Passing test: `bundle://proof/SB09/transcripts/final-validation.md`
- Changed source files: `bundle://proof/SB09/changed-file-hashes.md`
- Production assertions: process filter and full unit suite pass after migrations, typed contracts, bridge extraction, preflight, descriptor persistence, and template hardening.
- Red-team negative case: fake-proof closure without transcripts or source hashes is rejected by completed-stage bundle validation.
- Downstream dependency check: final closure has no downstream subbundle; live 5032 recovery is recorded as an environment blocker because no live app/process API access was available in this execution turn.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Final hardening proof set | SB02-SB08 manifests | completed validator and architecture gate | bundle closure lifecycle | red-team verifier rejects fake proof |
