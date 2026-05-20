# SB09 Proof Manifest

## Status

- Subbundle: `SB09 - Task-facing Recall Brief And Line-level Provenance`
- Status: `Completed`
- Owned requirements: `R-13`, `R-16`
- Raw notes: recall synthesis must plan task-facing statements, keep source/reference diagnostics hidden by default, and preserve statement-level lineage through aggregate claims and original source maps.
- Semantic invariant contract: `bundle://proof/SB09/semantic-invariants.md`

## Changed File Hashes

Complete after-change SHA-256 values are recorded in `bundle://proof/SB09/transcripts/changed-file-hashes.txt`.

Primary after-change SHA-256 values:

- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityContracts.cs`
  - SHA-256: `5036D8C2A92CACDC1D2B28B59A39B06EC5A361F20DFF89220BD7C7ABADE76B1F`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallBriefComposition.cs`
  - SHA-256: `B5F9C5ABD1620DB7BD78770768E1F71E49DFAF4E2E6B7CB569098383D4374DF5`
- `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs`
  - SHA-256: `272C40DD7C01A9F4D2D5397FF9FD3C5CE369EE0C8B2E3CA4D819854D141FBED7`
- `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityCollaboratorTests.cs`
  - SHA-256: `D307E65C6DB3A37404C004B12B5B7A11791C23547FBBC4F30B264D4CBDF23E1F`
- `bundle://proof/SB09/semantic-invariants.md`
  - SHA-256: `438CEB0BD06D72738F2CFC4765B47CCFDC45608443E385AD0634EC69B322C951`

## Command Transcripts

- Current failing-first transcript: `bundle://proof/SB09/transcripts/failing-first-current.txt`
- Passing transcript: `bundle://proof/SB09/transcripts/passing-semantic-tests.txt`
- Regression transcript: `bundle://proof/SB09/transcripts/regression-tests.txt`
- Source assertions transcript: `bundle://proof/SB09/transcripts/source-assertions.txt`
- Build transcript: `bundle://proof/SB09/transcripts/build.txt`
- No-migration proof transcript: `bundle://proof/SB09/transcripts/no-migration-proof.txt`
- Anti-stub audit transcript: `bundle://proof/SB09/transcripts/anti-stub-audit.txt`
- Hash transcript: `bundle://proof/SB09/transcripts/changed-file-hashes.txt`
- Prepared validator transcript: `bundle://proof/SB09/transcripts/prepared-validator-after-sb09.txt`

## Tests And Invariants

- Test name: `SemanticInvariant_RecallBriefKeepsAggregateClaimLineageAtStatementLineLevel`
- Test name: `RecallBriefComposer_ProducesTypedTaskFacingPlanKindsAndHidesDiagnostics`
- Test name: `RecallBriefComposer_WarnsWhenBudgetOmitsImportantCaveats`
- Test name: `RecallSynthesis_SeparatesConflictingClaimsIntoCaveatStatements`
- Test name: `ReferenceResolver_UsesPersistedAggregateClaimMapToAvoidSiblingClaimExpansion`
- Test name: `ReferenceResolver_ExpandsAggregateMemoryToOriginalSourceMaps`
- Test name: `ReferenceResolver_ExpandsFadedProfessorAnchorLineage`
- Test name: `RecallBriefComposer_SplitsApprovalConflictAndCarriesAggregateClaimIds`

Invariant IDs covered by transcripts:

- `SB09-RECALL-LINEAGE-01`
- `SB09-PLAN-KINDS-02`
- `SB09-BUDGET-AND-REFERENCE-03`

## Source Assertions

`bundle://proof/SB09/transcripts/source-assertions.txt` proves the strongly typed statement plan enum and contract are present, recall composer groups aggregate-backed statements by claim ID, conflict/caveat/missing-evidence/reference-hint plans are typed, internal score/source diagnostics are filtered from default brief text, `ReferencesShownByDefault` remains false, and persisted source-map rows carry `AggregateClaimId` for reference resolution.

## Red-Team Negative Proof

`bundle://proof/SB09/transcripts/passing-semantic-tests.txt` proves release approval and rollback owner aggregate claims remain separate statement lines with one claim/source lineage each. The same transcript proves explicit reference/debug requests expose only a reference hint while default briefs hide diagnostics and budget omissions warn before important caveats are silently dropped. `bundle://proof/SB09/transcripts/regression-tests.txt` proves collaborator conflict cases produce `Conflict` plan kinds instead of generic answers.

## Browser And Host Proof

Browser validation: N/A. SB09 changes backend recall synthesis, reference resolution, contracts, and unit tests only; no UI routes, components, host startup behavior, or browser-visible behavior changed.

## Persistence And Migration Proof

`bundle://proof/SB09/transcripts/no-migration-proof.txt` proves no SQLite or PostgreSQL migration files changed for SB09. `PlanKind` is a synthesized recall contract value, and line-level provenance reuses the existing `AggregateClaimId` statement source-map persistence.

## Shallow-Pass Traps

- Joining aggregate fragments into one statement with every source ref still fails `SB09-RECALL-LINEAGE-01`.
- Text-only prefixes without `CognitiveMemoryRecallStatementPlanKind` still fail `SB09-PLAN-KINDS-02`.
- Triggering reference hints from generated `source-backed` summaries still fails the explicit-reference-request negative case.
- Silently dropping caveats, conflicts, or missing-evidence plans under a budget still fails `SB09-BUDGET-AND-REFERENCE-03`.

## Downstream Dependency Check

`bundle://proof/SB09/transcripts/regression-tests.txt` reruns `CognitiveMemoryQualityFoundationTests`, `CognitiveMemoryQualityCollaboratorTests`, and faded professor reference resolution after the recall contract change. `bundle://proof/SB09/transcripts/build.txt` proves the cognitive-memory module builds after the contract update. `bundle://proof/SB09/transcripts/prepared-validator-after-sb09.txt` proves the bundle remains valid for prepared-stage progression after SB09 closure.
