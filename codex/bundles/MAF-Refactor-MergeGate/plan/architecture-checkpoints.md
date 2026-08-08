# Architecture checkpoints

| After | Required checkpoint | Downstream unlock | Reopen trigger |
|---|---|---|---|
| SB00 | Baseline snapshot health, exact owner/source inventory, failing-first blocker proof, no production diff | SB01 | A blocker cannot be reproduced or source ownership differs |
| SB01 | Tri-state authority result, fail-closed restoration, positive legacy proof, no provider construction on rejection | SB02 | Any continuation/restoration path bypasses the same validation |
| SB02 | Module-owned implementations, DI registry, no hard-coded provider construction, no new project cycle | SB03 | Source-kind product logic remains or returns to Modules.AgentFramework |
| SB03 | Exact effective context flows through guard, telemetry, approval and recoverability; MAF remains process-neutral | SB04 | Any downstream consumer still uses the original neutral context |
| SB04 | Trusted effective scope creates/disposes cleanup services; cross-scope durable lease proof passes | SB05 | Cleanup uses a fixed scope or retains conflicting leases as success |
| SB05 | Independent scoped stores share bounded coordination; temp files are cleaned on fault/cancel | SB06 | CAS proof uses only one store instance or overclaims cross-process safety |
| SB06 | Durable compensation and active-turn invariants pass failure/cancel/abandon/recovery paths | SB07 | Any ordinary failure leaves turn-owned state orphaned |
| SB07 | Checked attempt aggregation and typed failure usage reach workflow observations | SB08 | Any reported attempt usage is discarded or public errors leak provider details |
| SB08 | Production registration/consumer absent; library composes in isolation; no fallback activation | SB09 | Any production path resolves `ILlmConversationService` without profile fencing |
| SB09 | Post-change CodeAnalytics/dependency review, architecture review gate, builds/tests/smokes, verifier decision | Merge | Later proof contradicts any prerequisite or worktree/SHA differs from recorded state |

At every checkpoint, reject new partial-class expansion, service location in core behavior, hidden
fallbacks, unplanned references, fake separation, and tests that require the old broad owner to exercise
an extracted responsibility.
