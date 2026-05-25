# Input coverage matrix

| Raw input | Normalized requirements | Owning subbundles | Proof |
|---|---|---|---|
| Review what Codex fulfilled | R1, R7, R8 | SB01, SB08 | execution report and source assertions |
| Review what Codex skipped | R2-R6 | SB02-SB07 | subbundle manifests |
| Find SQLite-era DB bottlenecks | R2, R4, R6 | SB03, SB05, SB07 | bottleneck audit and benchmarks |
| Unlock bottlenecks | R4-R6 | SB05-SB07 | PostgreSQL concurrency tests |
| Preserve canonicality | R3, R5 | SB02, SB06 | semantic invariant tests |
| Prepare follow-up bundle | R7, R8 | all | bundle validator and final report |
