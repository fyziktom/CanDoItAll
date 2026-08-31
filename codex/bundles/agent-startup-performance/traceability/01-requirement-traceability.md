# Requirement And Input Traceability

| Raw input | Requirement | Owner | Planned proof | Closure path |
|---|---|---|---|---|
| N001: “only for those three first parts” | R01/R02/R03/R08 | SB01/SB02/SB03 | Three source-bounded diffs, focused tests, architecture review, performance | All three governed manifests |
| N002: fourth risky; accumulation/exception rationale | R04 | All; explicit exclusion | Diff audit retains every per-stage await/commit/flush; isolated failure/cancellation/recovery | Deferred recommendation4 remains excluded, never marked implemented |
| N003: “proper real (include playwright mcp via UI) testing in5032and5214” | R06/R07/R10 | SB03 integrated gate | UI01-UI06 both hosts, applicable approval cases, real tools/results, paired timings | Two-host browser/host manifests and independent verifier |
| N004: working today; no broken agent work/errors | R04/R05/R06 | All | Security, validated provider failures, projection equality, recovery, conversation/tool/history | Per-unit invariant proof and combined gate |
| N005: “do not start implementation” | R09 | Preparation | Git diff only bundle files; no builds/tests/live agent/deployment work in this turn | Preparation audit; execution Not started |

Each requirement appears in its owning subbundle. `reviews/01-execution-report.md` keeps implementation notes Not solved until proof exists; N005 can close for preparation while overall implementation remains unstarted. An excluded idea is not silently counted as a delivered optimization.
