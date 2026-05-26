# Structured Input

| Raw note | Exact wording | Requirement | Owning subbundle |
| --- | --- | --- | --- |
| RN01 | block unnecessarily | RQ01, RQ06 | SB01, SB04 |
| RN02 | complete with weak/manual artifact validation | RQ03, RQ08 | SB03, SB07, SB08 |
| RN03 | deny legitimate product mutation due to read-only alias overlap | RQ01 | SB01 |
| RN04 | allow script-based side effects through imperfect regex inspection | RQ05 | SB06, SB07 |
| RN05 | fail to deduplicate artifacts because projection identity is not fully materialized | RQ02 | SB02 |
| RN06 | infer wrong block/recovery classification from broad reason text | RQ04, RQ10 | SB05, SB10, SB11 |
| RN07 | route workflow/subprocess artifacts heuristically instead of explicitly | RQ07 | SB09, SB11 |
| RN08 | rely on workspace filesystem validation instead of the storage abstraction | RQ08 | SB08 |
| RN09 | add refactoring checkpoints every few subbundles | RQ11 | SB04, SB07, SB11 |
| RN10 | add generic red-team coverage across software and non-software processes | RQ12 | SB14 |
