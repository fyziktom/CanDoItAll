# SB063 Semantic Invariants

## SB063_INV_001 Final Red-Team Rejects Report-Only Closure
- Source raw note: SB061 requires rejection of report-only/live-skip/generic-host traps.
- Expected behavior: final closure remains blocked unless proof is command-backed, source-backed, manifest-backed, and validator-backed.
- Disallowed shallow implementation: marking SB061-SB063 passed from execution-report prose, prior summaries, or collapsed status rows without transcripts and source scans.
- Positive proof: `bundle://proof/SB061/transcripts/final-trap-unit-guards.txt`, `bundle://proof/SB061/transcripts/final-trap-source-assertions.txt`.
- Red-team negative case: `bundle://proof/SB063/transcripts/red-team-final-trap-rejection.txt`.

## SB063_INV_002 Live-Skip And Deterministic Proof Remain Separate From Live Provider Proof
- Expected behavior: the final disabled live process-run smoke path is classified only as skip-path proof; prior SB008 remains the live process-run provider proof; deterministic/fake-provider tests are not reported as live OpenAI evidence.
- Disallowed shallow implementation: claiming live-provider proof from a disabled live test, deterministic fallback matrix, or specialist-agent-only smoke.
- Positive proof: `bundle://proof/SB061/transcripts/final-live-process-run-skip-path.txt`, `bundle://proof/SB061/transcripts/final-trap-source-assertions.txt`.
- Downstream dependency check: final closure and handoff must preserve live/skipped/deterministic classification.

## SB063_INV_003 Generic Runtime Host Remains Denied
- Expected behavior: runtime-host prerequisites remain `Not satisfied`, premature surfaces remain `Blocked`, runtime-host status remains `Not approved`, and production source contains no generic runtime host, registry, selector, DI registration, manager command, scheduler/workflow hook, mutation permission, or Core dependency drift.
- Disallowed shallow implementation: treating diagnostics, audit readback, docs parity, or non-empty proof rows as permission to execute drivers.
- Source scan proof: `bundle://proof/SB062/transcripts/final-source-scans.txt`.
- Anti-stub audit: `bundle://proof/SB063/transcripts/gate-u-final-anti-stub-audit.txt`.

## Production Behavior Artifact Matrix
| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Final trap unit guards | SB061 unit matrix | Gate U manifest | Gate U proof index | Red-team rejects report-only closure |
| Live disabled path | SB061 live skip transcript | Final handoff classification | Gate U manifest | Red-team rejects live-skip-as-pass |
| Final source scans | SB062 source scans | Gate U and final closure | Gate U proof index | Anti-stub audit rejects hidden shortcuts |
| Generic-host denial | Docs and unit guards | Runtime-host migration posture | Gate U manifest | Red-team rejects diagnostics-as-approval |

## Gate Result
Gate U is semantically adequate for final red-team closure. Final traps for report-only proof, live-skip-as-pass, generic-host approval, non-empty diagnostics as approval, docs-only optimism, raw OpenAI key leakage, UI drift, Core dependency drift, and hidden runtime hooks are rejected by focused tests, source assertions, final scans, anti-stub audit, and red-team proof.
