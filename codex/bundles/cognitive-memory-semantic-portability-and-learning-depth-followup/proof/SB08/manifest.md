# SB08 Proof Manifest

## Status

- Subbundle: `SB08 - Event-backed Assimilation, Mastery, And Fading`
- Status: `Completed`
- Owned requirements: `R-11`, `R-12`, `R-16`
- Raw notes: professor anchor assimilation and fading must use durable accepted-use/integration events, not mastery keywords or recall source-map mentions; lifecycle transitions must be auditable and comparing anchors must be repaired when validation rejects the comparison.
- Semantic invariant contract: `bundle://proof/SB08/semantic-invariants.md`

## Changed File Hashes

Complete after-change SHA-256 values are recorded in `bundle://proof/SB08/transcripts/changed-file-hashes.txt`.

Primary after-change SHA-256 values:

- `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAssimilationEvaluator.cs`
  - SHA-256: `C447BC972962062483D7C8997726A9F20BCC21C40868EF424FDADB23FB18E42C`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorService.cs`
  - SHA-256: `C4D453A9DCBAE1DB05807EDBD4EFD0A55930F5ED611748B80AAE2EBD7B80B8FE`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorTransitionAudit.cs`
  - SHA-256: `488CAA1A4F6B8A05632E953F3051C58A6285BB3725908B53B93F0D7F8A7E2745`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedContracts.cs`
  - SHA-256: `DC8B638D3C2E689A3F1304A967AE5BE3EB27671B49EC9423AFB5D3F1F0457D4E`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Signals/CognitiveMemorySignalContracts.cs`
  - SHA-256: `A31B04616FF9D1A7A7360A8D254EF4B0E20421A8EFC8432D9CA5DC3D3E627D67`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamValidator.cs`
  - SHA-256: `332555A97AD8D62E8B97A7E17D2E2AF05424AB1295A81CC1540FE62215FDC5C5`
- `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs`
  - SHA-256: `3BDD7F5D9AC43C086B64C2BE06AD1DEE619FD60F951EDE1D0EB8D0ADB77279A7`
- `bundle://proof/SB08/semantic-invariants.md`
  - SHA-256: `D127E5A25D7532AA55AFC68C12C6E02E02B98000CA2704A2522829952B216420`

## Command Transcripts

- Historical failing-first transcript: `bundle://proof/SB02/transcripts/failing-first-semantic-corpus.txt`
- Current failing-first transcript: `bundle://proof/SB08/transcripts/failing-first-current.txt`
- Passing transcript: `bundle://proof/SB08/transcripts/passing-semantic-tests.txt`
- Regression transcript: `bundle://proof/SB08/transcripts/regression-tests.txt`
- Source assertions transcript: `bundle://proof/SB08/transcripts/source-assertions.txt`
- No-migration proof transcript: `bundle://proof/SB08/transcripts/no-migration-proof.txt`
- Anti-stub audit transcript: `bundle://proof/SB08/transcripts/anti-stub-audit.txt`
- Hash transcript: `bundle://proof/SB08/transcripts/changed-file-hashes.txt`
- Prepared validator transcript: `bundle://proof/SB08/transcripts/prepared-validator-after-sb08.txt`

## Tests And Invariants

- Test name: `SemanticInvariant_ProfessorAnchorScanRequiresAcceptedUseEventsInsteadOfSourceMapMentions`
- Test name: `ProfessorAnchor_ScanAssimilatesAndFadesIntegratedMasteryEvidence`
- Test name: `ProfessorAnchor_ScanRequiresAggregateReadyIntegration`
- Test name: `ProfessorAnchor_RejectedComparisonReturnsAnchorToActiveWithAudit`
- Test name: `ProfessorAnchor_ManualAssimilationRequiresReviewConfirmation`
- Test name: `ProfessorAnchor_AssimilationRequiresMasteryEvidenceBeyondIndependentSupport`
- Test name: `ProfessorAnchor_AssimilatesAndFadesOnlyAfterDerivedMemoryExists`
- Test name: `ProfessorAnchor_DirectCaptureMemoryCannotAssimilateItsOwnAnchor`
- Test name: `ProfessorAnchor_RejectsDescendantOnlyAggregateSupport`
- Test name: `ProfessorAnchor_FadeDemotesDirectCaptureMemory`
- Test name: `ReferenceResolver_ExpandsFadedProfessorAnchorLineage`
- Test name: `EndToEndProfessorCorrection_DreamsAssimilatesRecallsAndResolvesLineage`

Invariant IDs covered by transcripts:

- `SB08-EVENT-MASTERY-01`
- `SB08-INTEGRATION-02`
- `SB08-TRANSITION-AUDIT-03`

## Source Assertions

`bundle://proof/SB08/transcripts/source-assertions.txt` proves automatic assimilation counts `ProfessorAnchorAcceptedUse` signal events, rejects source-map-only recall mentions, requires aggregate-ready integration, avoids double-counting source/evidence links from the same derived memory, gates manual assimilation on `ManualReviewConfirmed`, and writes `ProfessorAnchorLifecycleTransition` signal rows for service and dream-validation transitions.

## Red-Team Negative Proof

`bundle://proof/SB08/transcripts/passing-semantic-tests.txt` proves source-map-only repeated recall mentions no longer assimilate or fade an active professor anchor. The same transcript proves accepted-use events plus a non-aggregate-ready cluster do not satisfy integration, and a rejected comparison returns the anchor to Active with `Active -> Comparing` and `Comparing -> Active` audit signals.

## Browser And Host Proof

Browser validation: N/A. SB08 changes backend professor assimilation, lifecycle transition auditing, signal enum semantics, dream validation repair, and unit tests only; no UI routes, components, host startup behavior, or browser-visible behavior changed.

## Persistence And Migration Proof

`bundle://proof/SB08/transcripts/no-migration-proof.txt` proves no EF DbContext, entity configuration, model snapshot, SQLite migration, or PostgreSQL migration files changed. SB08 reuses existing `CognitiveMemory_Signals` and `CognitiveMemory_ScoreEvaluationTraces` tables with new enum values only.

## Downstream Dependency Check

`bundle://proof/SB08/transcripts/regression-tests.txt` reruns the professor-anchor lifecycle, SB07 natural professor capture, default recall exclusion, faded-lineage reference resolution, and the end-to-end professor correction flow. `bundle://proof/SB08/transcripts/prepared-validator-after-sb08.txt` proves the bundle remains valid for prepared-stage progression after SB08 closure. SB09 can now rely on event-audited assimilated/faded professor anchors.
