# SB057 Semantic Invariants

## SB057_INV_001 Manager Diagnostics API Smoke Is Operator-Visible Without UI Drift
- Source raw note: SB055 permits manager diagnostics large-screen route or API proof.
- Expected behavior: the manager readback API serializes process-run identity, step identity, caller context, diagnostics, audit records, observation hash, and no-mutation flags for operator consumption.
- Disallowed shallow implementation: UI label-only proof, DTO-only type checks, or reusing earlier readback proof without asserting audit records and hash-bearing readback.
- Positive proof: `bundle://proof/SB055/transcripts/manager-diagnostics-api-smoke-focused-tests.txt`, `bundle://proof/SB055/transcripts/manager-diagnostics-api-source-assertions.txt`.
- UI boundary proof: `bundle://proof/SB057/transcripts/gate-s-operator-smoke-boundary-source-scan.txt` proves no UI route or Playwright source changed for the API-proof path.

## SB057_INV_002 Process Run Detail Readback Carries Verification Audit And Denial Metadata
- Source raw note: SB056 requires process run detail with verification audit readback.
- Expected behavior: process-run and step-scoped verification readback includes denial category, denial code, denial message, audit record identity, denied count, observation hash, and mutation-denial flags.
- Disallowed shallow implementation: success-only diagnostics proof, audit persistence without serialized readback, or denial metadata that is not tied to process-run and step identity.
- Positive proof: `bundle://proof/SB056/transcripts/process-run-detail-verification-audit-readback-focused-tests.txt`, `bundle://proof/SB056/transcripts/process-run-detail-verification-audit-source-assertions.txt`.
- Red-team negative case: `bundle://proof/SB057/transcripts/red-team-operator-smoke-shallow-proof-rejection.txt`.

## SB057_INV_003 Operator Smoke Does Not Expand Runtime Authority
- Expected behavior: Gate S adds focused operator API tests only; it does not add execution-capable drivers, runtime hooks, mutation permissions, Process Core dependencies, UI route changes, or bundle-path coupling.
- Disallowed shallow implementation: claiming an operator smoke as approval for process-driver execution, adding hidden mutation permissions, or using browser proof for an unchanged UI route.
- Source scan proof: `bundle://proof/SB057/transcripts/gate-s-operator-smoke-boundary-source-scan.txt`.
- Anti-stub audit: `bundle://proof/SB057/transcripts/gate-s-operator-smoke-anti-stub-audit.txt`.

## Production Behavior Artifact Matrix
| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Manager diagnostics API readback | SB055 focused test | Operator API smoke contract | Gate S proof index | Red-team rejects UI-label-only proof |
| Verification audit readback | SB056 focused test | Process-run detail readback contract | Gate S proof index | Red-team rejects success-only diagnostics proof |
| No UI drift API path | Gate S boundary source scan | Browser validation logging | Gate S manifest | Red-team rejects screenshot claims without UI change |
| Runtime authority boundary | Gate S boundary source scan | Final closure gates | Gate S manifest | Anti-stub audit rejects hidden shortcuts |

## Gate Result
Gate S is semantically adequate for operator smoke. The manager diagnostics API and process-run detail readback expose audit-backed, hash-bearing, mutation-free verification facts without changing UI routes or expanding runtime authority.
