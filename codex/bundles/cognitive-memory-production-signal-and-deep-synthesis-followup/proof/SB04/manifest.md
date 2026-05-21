# SB04 Proof Manifest

## Status

- Subbundle: `SB04 - Professor comparison review lifecycle`
- Status: `Completed`
- Owned requirements: `R04`
- Raw notes: Anchors in `Comparing` must be explicitly resolved and audited.
- Semantic invariant contract: `bundle://proof/SB04/semantic-invariants.md`

## Changed File Hashes

| File | SHA-256 |
|---|---:|
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedContracts.cs` | `A53AEC77C5C6463681DCDEE6C66694E7B473C314E625B517528810DBADE18FF8` |
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorReviewService.cs` | `4A14B7BFB3BA744B62C5811243ABF93297BAE595F9A2A08F7D7A1B17D5C79CA0` |
| `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` | `AA63276727CD6793DF76447A03F42E445431AB57774EF65D050B5D89231AE2CB` |

Full hash transcript: `bundle://proof/SB04/transcripts/changed-file-hashes.txt`.

## Command Transcripts

- Failing-first transcript: `bundle://proof/SB04/transcripts/failing-first.txt`
- Passing transcript: `bundle://proof/SB04/transcripts/passing.txt`
- Source assertions transcript: `bundle://proof/SB04/transcripts/source-assertions.txt`
- Anti-stub audit transcript: `bundle://proof/SB04/transcripts/anti-stub.txt`
- Changed-file hashes: `bundle://proof/SB04/transcripts/changed-file-hashes.txt`

## Tests And Invariants

- Test name: `SemanticInvariant_ProfessorComparisonReviewResolutionIsExplicitAndAudited`
- Test name: `ProfessorComparisonReviewResolution_ReturnsComparingAnchorToActiveAndAuditsTransition`
- Invariant ID: `SB04-COMPARISON-RESOLUTION-01`
- Invariant ID: `SB04-AUDITED-TRANSITION-02`

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `Comparing` anchor state resolution | `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorReviewService.cs` implements `ResolveComparisonAsync` | `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` exercises the production service and persisted state | `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorTransitionAudit.cs` records the lifecycle transition signal | SB02 red baseline proves no resolver existed; invalid non-`Comparing` state throws |
| `ProfessorAnchorLifecycleTransition` signal | `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorTransitionAudit.cs` produces the audit signal | `ProfessorComparisonReviewResolution_ReturnsComparingAnchorToActiveAndAuditsTransition` verifies persisted audit metadata | `ResolveComparisonAsync` calls the audit path for every state transition | No state change means the audit helper cannot create a misleading transition |

## Source Assertions

`bundle://proof/SB04/transcripts/source-assertions.txt` records the outcome enum, request/result records, `ResolveComparisonAsync` interface method, state validation, explicit actor/reason handling, derived-memory validation for accept outcomes, and transition audit call.

## Red-Team Negative Proof

`bundle://proof/SB04/transcripts/failing-first.txt` cites the SB02 red baseline where the review service lacked a comparison resolver. The implementation now rejects attempts to resolve anchors not in `Comparing`.

## Browser And Host Proof

Browser validation: N/A. SB04 is a backend lifecycle service change.

## Downstream Dependency Check

SB10 can resolve reviewable professor comparisons deterministically before assimilation/fade.
