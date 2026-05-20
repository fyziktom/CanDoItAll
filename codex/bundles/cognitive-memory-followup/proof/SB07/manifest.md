# SB07 Proof Manifest - Assimilation mastery and fading lifecycle

## Subbundle

- Subbundle: `07-07-assimilation-mastery-and-fading-lifecycle`
- Status: `Completed`
- Owned requirements: `R-13`, `R-14`
- Owned raw note: `Professor guidance must be internalized through mastery evidence and faded from ordinary recall only after the direct quote is demoted while lineage remains resolvable`
- Browser/host proof: `N/A - backend professor-anchor lifecycle, recall, and reference-lineage tests only`
- Test name: `ProfessorAnchor_RejectsDescendantOnlyAggregateSupport`
- Test name: `ProfessorAnchor_FadeDemotesDirectCaptureMemory`
- Test name: `ProfessorAnchor_ScanAssimilatesAndFadesIntegratedMasteryEvidence`
- Test name: `ReferenceResolver_ExpandsFadedProfessorAnchorLineage`
- Test name: `EndToEndProfessorCorrection_DreamsAssimilatesRecallsAndResolvesLineage`

## Changed Files And Hashes

| File | SHA-256 |
|---|---:|
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAdvancedContracts.cs` | `8F8946B88210C7CCD85F6F2FB8399BB911558BBB1953FC56035126000478E27E` |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryProfessorAssimilationEvaluator.cs` | `A3EADE6CF31B41711E3A7809E554018CC6920023C073858DC09B247413224202` |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryProfessorAnchorService.cs` | `222861E073CACA71B3332BF5A0D1E7ABEC8BB034C3605EDE9494726A93EB6E37` |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\CognitiveMemoryModuleServiceCollectionExtensions.cs` | `3E2CE71F6F44FB1D15CA9E1A83A17B21DDAED0A264C097E3D47841F18400773E` |
| `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryAdvancedServicesTests.cs` | `ADC37EBA9A5C5CF0B34EF813DB1EFD06ED81720759AE88CEAE20CAFF93DF4A53` |

## Proof Artifacts

- Failing-first transcript: `proof/SB03/transcripts/failing-first-targeted-tests.txt`
- Passing transcript: `proof/SB07/transcripts/passing-targeted-lifecycle-tests.txt`
- Regression transcript: `proof/SB07/transcripts/passing-professor-lifecycle-regression-tests.txt`
- Source assertion transcript: `proof/SB07/transcripts/source-assertions.txt`
- Anti-stub audit transcript: `proof/SB07/transcripts/anti-stub-audit.txt`
- Bundle prepared-stage validator transcript: `proof/SB07/transcripts/prepared-validator-after-sb07.txt`

## Source Assertions

- `CognitiveMemoryProfessorAssimilationEvaluator.cs` rejects direct capture self-assimilation, builds anchor descendant memory ids through dream aggregate source maps, and counts only independent non-descendant support as assimilation proof.
- `CognitiveMemoryProfessorAssimilationEvaluator.cs` requires mastery evidence and, for scan-driven assimilation, repeated successful use plus dream or cluster integration evidence.
- `CognitiveMemoryProfessorAnchorService.cs` adds `ScanAssimilationAsync` for deterministic scheduled/manual assimilation scans and delegates all assimilation decisions through the evaluator.
- `CognitiveMemoryProfessorAnchorService.cs` demotes faded direct capture memory and claims to `Retired`/`Deprecated` while preserving the assimilated memory id for reference-on-demand lineage.
- `CognitiveMemoryModuleServiceCollectionExtensions.cs` registers the assimilation evaluator for normal DI construction.

## Semantic Adequacy

- Raw note owned: professor guidance must be kept temporarily, compared against independent mastery evidence, then faded only after it is internalized.
- Shipped behavior: assimilation is evaluator-driven, blocks direct/self and descendant-only aggregate support, requires mastery signals, repeated use, and dream/cluster integration for scan-driven fading, and retires the direct quote memory once faded.
- Shallow-pass trap: flipping the anchor state to `Assimilated` or `Faded` would pass state-only assertions while still allowing anchor-derived aggregates to count as independent proof and leaving the direct professor quote active in ordinary recall.
- Adversarial negative proof: SB03 failing-first transcript shows the mastery-gated professor assimilation scenario failed before lifecycle hardening.
- Semantic positive proof: SB07 targeted transcript shows descendant-only support rejection, direct quote demotion, and automatic scan assimilation/fading pass; the regression transcript keeps faded-lineage reference resolution and the end-to-end professor correction flow green.
- Anti-stub audit: `proof/SB07/transcripts/anti-stub-audit.txt` finds no TODO, NotImplemented, or fixture/test-name-specific production branches in SB07 production files.

## Progression Decision

SB07 closure passes. SB08 may rely on active/faded professor anchor handling: direct quote memories are removed from ordinary recall after fading, while reference resolution can still explain the professor lineage on demand.
