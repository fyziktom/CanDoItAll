# Finding traceability

| Input finding | Normalized requirement | Owning work | Planned proof | Closure surface |
|---|---|---|---|---|
| MRG-001 | Malformed/current authority and identity mismatch fail closed; positive-evidence legacy remains compatible | SB00, SB01 | Failing-first parser/restoration tests, positive legacy case, source assertion | `05-EXECUTION-STATUS.md`, SB01 proof |
| MRG-002 | Source authority implementations and DI registration belong to publishing modules | SB00, SB02 | Pre/post dependency map, registration/duplicate/unknown tests, architecture gate | `05-EXECUTION-STATUS.md`, SB02 proof |
| MRG-003 | Pipeline returns and all consumers use the contributor-enriched effective context | SB00, SB03 | Failing-first recoverable-denial/enrichment tests, source and telemetry assertions | `05-EXECUTION-STATUS.md`, SB03 proof |
| MRG-004 | Terminal cleanup uses the trusted effective run scope | SB00, SB04 | Real durable project lease under organization run storage; success/failure/continuation/idempotency tests | `05-EXECUTION-STATUS.md`, SB04 proof |
| MRG-005 | File-store CAS serializes across independent scoped instances sharing canonical storage | SB00, SB05 | Cross-instance create/replace/delete races and temp-file cleanup | `05-EXECUTION-STATUS.md`, SB05 proof |
| MRG-006 | Failed, cancelled, abandoned, or recovered Adopt restores provider and acceleration | SB00, SB06 | Failing-first compensation and crash-recovery tests | `05-EXECUTION-STATUS.md`, SB06 proof |
| MRG-007 | Rename is rejected while a turn is active without state mutation | SB00, SB06 | Concurrent active-turn negative test | `05-EXECUTION-STATUS.md`, SB06 proof |
| MRG-008 | A complete turn reserves two transcript slots before provider invocation | SB00, SB06 | Near-capacity negative test proving zero provider calls | `05-EXECUTION-STATUS.md`, SB06 proof |
| MRG-009 | Usage is accumulated across every reported provider attempt and typed failure | SB00, SB07 | Empty/success, empty/empty, provider/deadline failure, overflow/negative, workflow projection tests | `05-EXECUTION-STATUS.md`, SB07 proof |
| MRG-010 | Ordinary conversations remain a tested dormant foundation until profile fencing exists | SB00, SB08 | No-consumer/registration architecture proof and isolated composition test | `05-EXECUTION-STATUS.md`, SB08 proof |
| MRG-011 | Fresh independent build, tests, guards, smokes, and exact merge decision | SB00, SB09 | Governed transcripts, hashes, red-team verification, final SHA/worktree | `05-EXECUTION-STATUS.md`, `reviews/FINAL-MERGE-DECISION.md` |
