# SB08 Proof Manifest - Recall task brief and claim lineage

## Subbundle

- Subbundle: `08-08-recall-task-brief-and-claim-lineage`
- Status: `Completed`
- Owned requirements: `R-15`, `R-16`
- Owned raw note: `Recall must produce concise task-facing synthesis by default and precise statement-to-claim-to-source lineage only on request`
- Browser/host proof: `N/A - backend recall synthesis, reference resolver, and persistence tests only`
- Test name: `RecallSynthesis_BuildsQueryShapedBriefInsteadOfTitleGroupedConcatenation`
- Test name: `RecallSynthesis_SeparatesConflictingClaimsIntoCaveatStatements`
- Test name: `ReferenceResolver_LimitsAggregateExpansionToRequestedClaimLineage`
- Test name: `ReferenceResolver_UsesPersistedAggregateClaimMapToAvoidSiblingClaimExpansion`
- Test name: `ReferenceResolver_DeniesRestrictedReferenceWithoutLocatorOrSummary`
- Test name: `ReferenceResolver_ExpandsFadedProfessorAnchorLineage`
- Test name: `QualityPersistenceModel_RegistersClustersDreamAggregatesValidationAndSynthesisTables`
- Test name: `Bootstrap_migrates_a_new_managed_sqlite_database`

## Changed Files And Hashes

| File | SHA-256 |
|---|---:|
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryRecallSynthesisService.cs` | `4ECE53C6D360385ED4191BF39FBA7B01F635D8231BAF29A1A0682DE2F2718DA2` |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryReferenceResolver.cs` | `F4E468D78CA1E31731B5E63E1F2252941A713EC55B73DB0E519076D81E084870` |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualityContracts.cs` | `FFC46469095709BA91102C297D0BF6429A0DEE86FB03041C97C1C75CD3C2B411` |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualityEntities.cs` | `520957404B4946F7B1CC8213624ABEA209E38F181B879C754AAA760387CC3119` |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualityEntityConfigurations.cs` | `A2E3AB0030C98CB2C3CEA7DE15368B72F88964C010A414597A3E77C7DE5D4F67` |
| `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryQualityFoundationTests.cs` | `29739152903341F3BCCC56153D1D776EC7709388E6B59B938A67BF3B1E616891` |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.Sqlite\Migrations\20260520190244_AddCognitiveMemoryStatementAggregateClaimMaps.cs` | `E60E2F9569EC5BD6FCF4FE9226E8404872688E4004D925A9E7ED1942F26DA22D` |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.Sqlite\Migrations\20260520190244_AddCognitiveMemoryStatementAggregateClaimMaps.Designer.cs` | `7EECA975EAC1AA8CB757A84D67ED79149EB87B83655F42751312302CFCB26E62` |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.Sqlite\Migrations\AppDbContextModelSnapshot.cs` | `540C8419ABCE4935782D76633790EDC10D9A9BAD07B7B336ADCE1DD11A05E9D7` |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.PostgreSql\Migrations\20260520190312_AddCognitiveMemoryStatementAggregateClaimMaps.cs` | `A2721A7C0495C8ABA051F7876EF706AE36E75D76CDE345F1CD98FAA4CB060CC4` |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.PostgreSql\Migrations\20260520190312_AddCognitiveMemoryStatementAggregateClaimMaps.Designer.cs` | `19C614D7A17064D30283097E082218E7B63083121EAF580EAFC4C83A182A63AB` |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.PostgreSql\Migrations\AppDbContextModelSnapshot.cs` | `8E5810346A5F351767EC2637E6A3EDECD1EB4F115C8AB9894EFA5E98408902D1` |

## Proof Artifacts

- Failing-first transcript: `proof/SB08/transcripts/failing-first-targeted-recall-reference-tests.txt`
- Passing transcript: `proof/SB08/transcripts/passing-targeted-recall-reference-tests.txt`
- Regression transcript: `proof/SB08/transcripts/passing-quality-professor-regression-tests.txt`
- Persistence/migration passing transcript: `proof/SB08/transcripts/passing-persistence-migration-smoke-tests.txt`
- Source assertion transcript: `proof/SB08/transcripts/source-assertions.txt`
- Anti-stub audit transcript: `proof/SB08/transcripts/anti-stub-audit.txt`
- Bundle prepared-stage validator transcript: `proof/SB08/transcripts/prepared-validator-after-sb08.txt`

## Source Assertions

- `CognitiveMemoryRecallSynthesisService.cs` composes query-shaped answer/action statements, splits detected approval conflicts into separate `Conflict caveat` statements, records omitted detail count warnings when the statement budget trims groups, and keeps references hidden by default.
- `CognitiveMemoryRecallSynthesisService.cs` loads persisted aggregate claim ids from selected context sections and stores `AggregateClaimId` on synthesized statement source maps so each statement carries claim-level lineage.
- `CognitiveMemoryReferenceResolver.cs` expands aggregate sources only through mapped aggregate claim ids when the synthesized statement has them; it does not expand sibling aggregate claims for the same aggregate memory.
- `CognitiveMemoryQualityEntities.cs`, `CognitiveMemoryQualityEntityConfigurations.cs`, and both provider migrations add nullable `AggregateClaimId` persistence plus indexes and a foreign key to dream aggregate claims.
- `CognitiveMemoryQualityFoundationTests.cs` includes conflict/caveat synthesis proof, restricted-reference redaction proof, faded professor lineage proof, and a sibling-claim negative resolver proof.

## Semantic Adequacy

- Raw note owned: recall must stop being fragment concatenation and must provide concise default output with exact claim/source lineage only on demand.
- Shipped behavior: synthesis now builds task-facing answer/action statements, emits separate conflict caveats instead of merging contradictory approval claims, persists statement-to-aggregate-claim maps, hides references by default, and resolver expansion follows only the requested statement's mapped claim lineage.
- Shallow-pass trap: adding the word `conflict` to a joined fragment or filtering references only by aggregate memory id would pass superficial tests while still merging contradictory claims and exposing every sibling source for a statement.
- Adversarial negative proof: SB08 failing-first transcript shows the conflict/caveat synthesis test failed before implementation; SB03 also captured the earlier recall/reference shallow failures.
- Semantic positive proof: targeted transcript shows query-shaped briefs, conflict separation, on-demand references, restricted redaction, faded professor lineage, and sibling-claim filtering pass; persistence/migration transcript proves the new claim map column is registered and migrates for SQLite bootstrap.
- Anti-stub audit: `proof/SB08/transcripts/anti-stub-audit.txt` finds no TODO, NotImplemented, or fixture/test-name-specific production branches in SB08 production files.

## Progression Decision

SB08 closure passes. SB09 may refactor around recall synthesis and reference resolver behavior, and SB10 may rely on reference-on-demand resolving each synthesized sentence through the exact mapped aggregate claim when available.
