# SB05 Proof Manifest - Dream synthesis and entailment validation

## Subbundle

- Subbundle: `05-05-dream-synthesis-and-entailment-validation`
- Status: `Completed`
- Owned requirements: `R-08`, `R-09`, `R-10`
- Owned raw note: `Dreaming must sort memories, create useful aggregates, validate them, and avoid suspiciously fast shallow completion`
- Browser/host proof: `N/A - backend dream/validator/apply tests only`
- Test name: `DreamRun_IntegratesComplementaryProcedureClaimsIntoSingleAggregateStatement`
- Test name: `DreamRun_ProducesModeSpecificStructuredOutputsBeyondTitlePrefix`
- Test name: `DreamValidation_RejectsNegatedClaimDespiteHighTokenOverlap`

## Changed Files And Hashes

| File | SHA-256 |
|---|---:|
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryDreamConsolidationService.cs` | `2BE2F82909393D2394D9785E4A7DCB1C7856FD9C1825259E0F419661295BAC0B` |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryDreamValidator.cs` | `75AE67EFA8781F82FE3D66D86D2D9C6D49A5678ED9D288673991DD8CA551FC0B` |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryDreamSynthesis.cs` | `ADA38BF460D145947488CA514146FEA45E00B44FE14CF654F817D800F6F6D97F` |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryAggregateConfidenceCalibrator.cs` | `31B051AA47EC238CEC69975AFCB36DC43724B03EC2A97CA4CB8F9D23CA1F85EA` |

## Proof Artifacts

- Failing-first transcript: `proof/SB03/transcripts/failing-first-targeted-tests.txt`
- Passing transcript: `proof/SB05/transcripts/passing-targeted-dream-tests.txt`
- Regression transcript: `proof/SB05/transcripts/passing-dream-apply-regression-tests.txt`
- Source assertion transcript: `proof/SB05/transcripts/source-assertions.txt`
- Anti-copy quality transcript: `proof/SB05/transcripts/anti-copy-quality-check.txt`
- Anti-stub audit transcript: `proof/SB05/transcripts/anti-stub-audit.txt`
- Bundle prepared-stage validator transcript: `proof/SB05/transcripts/prepared-validator-after-sb05.txt`

## Source Assertions

- `CognitiveMemoryDreamSynthesis.cs` adds deterministic dream claim synthesis and entailment validation collaborators.
- `CognitiveMemoryDreamConsolidationService.cs` persists `quality-dream-v3-claim-synthesis`, groups complementary source claims by aggregate subject, produces mode-specific schemas, and keeps claim-level source maps.
- `CognitiveMemoryDreamValidator.cs` validates aggregate claims against collective mapped source evidence, rejects bypass/negation conflicts, and flags representative-copy aggregates.
- `CognitiveMemoryAggregateConfidenceCalibrator.cs` now requires both broad source breadth and strongest-claim support before promotion to `StrongAccept`.

## Semantic Adequacy

- Raw note owned: dreams must create useful validated aggregates, not representative source copies.
- Shipped behavior: complementary claims are integrated into one aggregate statement, `ProcedureMining` and `FailureLearning` emit distinct structured sections, and unsupported negated claims are routed to review through claim-level entailment validation.
- Shallow-pass trap: representative claim selection can make non-empty aggregate records and source maps while still copying one source sentence and accepting high-token-overlap contradictions.
- Adversarial negative proof: SB03 failing-first transcript shows the three SB05 tests failed before this production change.
- Semantic positive proof: SB05 targeted transcript shows the same tests pass; regression transcript keeps 20 dream, validator, aggregate apply, and confidence calibration tests green.
- Anti-copy proof: `anti-copy-quality-check.txt` cites the integrated-claim assertion and representative-copy validator guard.
- Anti-stub audit: `anti-stub-audit.txt` finds no TODO, NotImplemented, or fixture/test-name-specific production branches in the SB05 production files.

## Progression Decision

SB05 closure passes. SB07 and SB08 may rely on synthesized aggregate claim/source maps, while SB09 must later register/version the new collaborators.
