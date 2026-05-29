# SB08 semantic invariants

Status: Completed

## Final Regression Contract

- Invariant ID: `SB08-FINAL-REGRESSION-CLOSURE`
- Source raw note: R12 requires concise, reproducible final evidence and R1-R11 require final regression coverage.
- Expected behavior: final proof cites targeted unit, integration, component, build, source assertion, browser, CI metadata, and red-team artifacts with explicit residual risks.
- Disallowed shallow implementation: marking the bundle complete from prose-only evidence or hiding missing durable/live-effect proof as success.
- Failing-first test: N/A for this process-only closure; behavior-changing subbundles SB01-SB07 captured failing-first proof.
- Passing test: `bundle://proof/SB08/transcripts/unit-targeted-regression.txt`, `bundle://proof/SB08/transcripts/integration-targeted-regression.txt`, and `bundle://proof/SB08/transcripts/component-targeted-regression.txt` passed.
- Changed source files: `repo://codex/bundles/workflow-maf-hardening-followup/architecture/03-maf-executor-binding-decision.md`, `repo://codex/bundles/workflow-maf-hardening-followup/reviews/02-final-architecture-review.md`, `repo://codex/bundles/workflow-maf-hardening-followup/reviews/01-execution-report.md`, `repo://docs/workflow-maf-hardening.md`, and proof files listed in `bundle://proof/SB08/manifest.md`.
- Production assertions: `bundle://proof/SB08/transcripts/source-assertions-risky-invariants.txt` verifies risky boundaries and defaults.
- Red-team negative case: `bundle://proof/SB08/final-verifier-red-team.md` audits R1-R12 and records residual follow-up triggers.
- Downstream dependency check: `bundle://proof/SB08/transcripts/final-build.txt` and final bundle validator proof close the downstream gate.

- R1-R12 must be traceable to source-level tests, command transcripts, browser proof where UI changed, or an explicit residual follow-up.
- Default validation must not require live Gmail, Office365, Docker, host-command, DurableTask, or Azure Functions execution.
- Evidence must be reproducible from targeted commands and must not depend on manually seeded production signals.

## Closure Invariants

- MAF package baseline is upgraded and verified on the selected 1.8 line.
- Dynamic `BindAsExecutor` remains an explicit adapter decision for graph-authored workflows.
- HITL, approval, events, checkpoints, payload policy, plugin governance, and backend honesty stay covered by targeted unit/integration/component tests.
- Browser validation remains attached to SB07, the only UI-affecting subbundle in the follow-up.
- Residual risks are explicit and have owner categories and follow-up triggers.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Proof |
| --- | --- | --- | --- | --- |
| Targeted regression matrix | `dotnet test` | final closure gate | Runs after all feature subbundles complete. | `bundle://proof/SB08/transcripts/unit-targeted-regression.txt`; `bundle://proof/SB08/transcripts/integration-targeted-regression.txt`; `bundle://proof/SB08/transcripts/component-targeted-regression.txt` |
| Source invariant scan | `rg` | closure review | Captures risky boundary symbols and defaults. | `bundle://proof/SB08/transcripts/source-assertions-risky-invariants.txt` |
| Final architecture review | SB08 docs | future maintainers | Records accepted design and residual risks. | `bundle://reviews/02-final-architecture-review.md` |
| Final verifier audit | SB08 proof | bundle owner | Checks R1-R12 adversarially before closure. | `bundle://proof/SB08/final-verifier-red-team.md` |
