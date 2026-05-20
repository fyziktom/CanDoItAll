# SB09 Semantic Invariants

## Invariant SB09-RECALL-LINEAGE-01

- Invariant ID: `SB09-RECALL-LINEAGE-01`
- Source raw note: Recall synthesis must produce task-facing briefs with line-level provenance.
- Expected behavior: Aggregate-backed recall statements keep exact statement-to-claim-to-source lineage so separate aggregate claims do not collapse into one line with combined provenance.
- Disallowed shallow implementation: Joining selected fragments into one statement and attaching every source ref or aggregate claim ID to the combined line.
- Failing-first test: `SemanticInvariant_RecallBriefKeepsAggregateClaimLineageAtStatementLineLevel` in `bundle://proof/SB09/transcripts/failing-first-current.txt` failed with the aggregate-backed release approval and rollback owner claims collapsed into one statement.
- Passing test: `SemanticInvariant_RecallBriefKeepsAggregateClaimLineageAtStatementLineLevel`, `ReferenceResolver_ExpandsAggregateMemoryToOriginalSourceMaps`, `ReferenceResolver_UsesPersistedAggregateClaimMapToAvoidSiblingClaimExpansion`, and `ReferenceResolver_ExpandsFadedProfessorAnchorLineage` in `bundle://proof/SB09/transcripts/passing-semantic-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallBriefComposition.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityContracts.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryReferenceResolver.cs`, and `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs`.
- Production assertions: `bundle://proof/SB09/transcripts/source-assertions.txt` proves aggregate claim IDs are used as statement group keys, persisted source-map rows carry `AggregateClaimId`, and reference resolution filters aggregate source maps to the requested claim lineage.
- Red-team negative case: Release approval and rollback owner statements must remain separate, each with one aggregate claim ID and one source ref; resolving one statement must not expand sibling aggregate claims.
- Downstream dependency check: `bundle://proof/SB09/transcripts/regression-tests.txt` reruns cognitive-memory quality and collaborator recall/reference tests.

## Invariant SB09-PLAN-KINDS-02

- Invariant ID: `SB09-PLAN-KINDS-02`
- Source raw note: Recall briefs must be task-facing answer/action/caveat/conflict/missing-evidence plans, not first useful source lines.
- Expected behavior: Each synthesized statement has a strongly typed plan kind: `Answer`, `Action`, `Caveat`, `Conflict`, `MissingEvidence`, or `ReferenceHint`. Conflicting statements are surfaced as conflicts, evidence-free useful text is marked missing evidence, and explicit debug/provenance/reference requests get a reference hint.
- Disallowed shallow implementation: Prefixing text with labels while leaving the contract untyped, or collapsing conflict/caveat/reference-hint handling into generic answer statements.
- Failing-first test: The pre-SB09 contract had no `CognitiveMemoryRecallStatementPlanKind`, so typed plan assertions in `RecallBriefComposer_ProducesTypedTaskFacingPlanKindsAndHidesDiagnostics` could not pass before the implementation.
- Passing test: `RecallBriefComposer_ProducesTypedTaskFacingPlanKindsAndHidesDiagnostics`, `RecallSynthesis_SeparatesConflictingClaimsIntoCaveatStatements`, and collaborator conflict tests in `bundle://proof/SB09/transcripts/passing-semantic-tests.txt` and `bundle://proof/SB09/transcripts/regression-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityContracts.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallBriefComposition.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs`, `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs`, and `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityCollaboratorTests.cs`.
- Production assertions: `bundle://proof/SB09/transcripts/source-assertions.txt` proves the enum, synthesized statement contract, plan-kind resolution, conflict plan creation, and source/reference hint plan creation are present.
- Red-team negative case: A generated source-backed summary must not trigger a reference hint by containing the word `source`; only explicit debug, provenance, citation, lineage, reference, inspect, show, include, open, or resolve requests do.
- Downstream dependency check: Existing recall synthesis tests in `bundle://proof/SB09/transcripts/regression-tests.txt` verify callers consume the typed plan kind without exposing internal score text.

## Invariant SB09-BUDGET-AND-REFERENCE-03

- Invariant ID: `SB09-BUDGET-AND-REFERENCE-03`
- Source raw note: Recall must hide scores/internal diagnostics by default, preserve exact provenance, and warn when budget drops important detail.
- Expected behavior: Brief text omits score/internal diagnostic/source locator details by default, `ReferencesShownByDefault` stays false, omitted caveat/conflict/missing-evidence plans produce warnings, and reference details remain available through explicit statement reference resolution.
- Disallowed shallow implementation: Exposing belief scores or locators in the default brief, silently truncating caveats, or expanding every aggregate sibling when resolving a single statement.
- Failing-first test: The pre-SB09 lineage failure in `bundle://proof/SB09/transcripts/failing-first-current.txt` demonstrates the old implementation did not preserve exact statement-level provenance under aggregate summarization.
- Passing test: `RecallBriefComposer_WarnsWhenBudgetOmitsImportantCaveats`, `RecallBriefComposer_ProducesTypedTaskFacingPlanKindsAndHidesDiagnostics`, and reference resolver lineage tests in `bundle://proof/SB09/transcripts/passing-semantic-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallBriefComposition.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryReferenceResolver.cs`, and `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs`.
- Production assertions: `bundle://proof/SB09/transcripts/source-assertions.txt` proves `ReferencesShownByDefault` remains false, source-map persistence includes aggregate claim IDs, and budget warnings include omitted important caveat/conflict/missing-evidence detail.
- Red-team negative case: Restricted references must not expose locator/summary without policy; default briefs must not display belief scores, internal diagnostics, or source locators.
- Downstream dependency check: `bundle://proof/SB09/transcripts/regression-tests.txt` covers quality foundation and collaborator regressions after the recall composer contract change.
