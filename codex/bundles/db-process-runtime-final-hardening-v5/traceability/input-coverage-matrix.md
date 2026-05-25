# Input coverage matrix

| Raw input | Requirement(s) | Subbundle(s) | Proof |
|---|---|---|---|
| Review fulfilled/skipped work | R1, R2, R8 | SB01, SB08 | execution report |
| Check for SQLite remnants | R2 | SB01, SB08 | residue audit |
| Inspect process DB work | R3-R7, R9 | SB02-SB07 | source + tests |
| Remove DB bottlenecks | R6, R7 | SB05, SB06 | query plans + benchmark |
| Preserve canonicality | R3-R5, R9 | SB02-SB04, SB07 | red-team tests |
| Prepare follow-up bundle | all | all | bundle structure |
