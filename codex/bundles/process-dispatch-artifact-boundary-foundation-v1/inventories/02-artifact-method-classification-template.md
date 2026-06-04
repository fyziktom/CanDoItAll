# Artifact Method Classification Template

Codex must fill this table in SB02 from real source.

| Method/Region | Source file | Category | Inputs | Outputs | Side effects | Migration candidate? | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `MatchExpectedArtifactId` strong-match branch | `ProcessRunAutomationDispatchService.ArtifactValidation.cs` | expectation-matching | expected artifacts, execution artifact metadata | matched expectation id | none | migrated | Strong-match disambiguation moved to `ProcessArtifactExpectationMatcher`. |
| `BuildExternalReferenceKey` | `ProcessRunAutomationDispatchService.ArtifactValidation.cs` | lineage-key-generation | execution artifact id | source external reference key | none | migrated | Delegates to `ProcessArtifactProjectionPlanner.BuildExecutionArtifactExternalReferenceKey`. |
| `BuildProcessMockArtifactExternalReferenceKey` | `ProcessRunAutomationDispatchService.ArtifactValidation.cs` | lineage-key-generation | step run id, expectation id, relative path | source external reference key | none | migrated | Planner now owns normalized process-mock key generation. |
| `BuildWorkspaceWrittenArtifactExternalReferenceKey` | `ProcessRunAutomationDispatchService.ArtifactValidation.cs` | lineage-key-generation | execution run id, expectation id, relative path | source external reference key | none | migrated | Planner now owns normalized workspace-write key generation. |
| `BuildExistingManagedArtifactExternalReferenceKey` | `ProcessRunAutomationDispatchService.ArtifactValidation.cs` | lineage-key-generation | execution run id, expectation id, relative path | source external reference key | none | migrated | Planner now owns existing-managed key generation. |
| `BuildResponseTextArtifactExternalReferenceKey` | `ProcessRunAutomationDispatchService.ArtifactValidation.cs` | lineage-key-generation | execution run id, relative path | source external reference key | none | migrated | Planner now owns assistant-response key generation. |
| `BuildProviderNativeBrowserArtifactExternalReferenceKey` | `ProcessRunAutomationDispatchService.ArtifactValidation.cs` | lineage-key-generation | execution run id, relative path | source external reference key | none | migrated | Planner now owns provider-native browser key generation. |
| `ApplyArtifactProjectionLineage` | `ProcessRunAutomationDispatchService.ArtifactProjection.cs` | lineage-key-generation | source key, execution run id, recovery context | compact external reference key | none | migrated | Delegates to `ProcessArtifactProjectionLineageBuilder`. |
| `BuildArtifactProjectionLineage` | `ProcessRunAutomationDispatchService.ArtifactProjection.cs` | lineage-key-generation | source kind, run ids, source artifact id | `ProcessArtifactProjectionLineage` | none | migrated | Delegates to `ProcessArtifactProjectionLineageBuilder`. |
| `BuildArtifactProjectionProvenance` | `ProcessRunAutomationDispatchService.ArtifactProjection.cs` | lineage-key-generation | base provenance, execution run id, recovery context | provenance text | none | migrated | Delegates to `ProcessArtifactProjectionLineageBuilder`. |
| `ProjectExecutionArtifactsAsync` planning portion | `ProcessRunAutomationDispatchService.ArtifactProjection.cs` | projection-source-discovery | candidate, execution detail, artifact file content | `ProcessArtifactProjectionPlan` | file read before planner; storage/DB after planner | migrated | First concrete production projection path migrated through planner. |
| `ProjectExecutionArtifactsAsync` storage/recording portion | `ProcessRunAutomationDispatchService.ArtifactProjection.cs` | storage-db-recording | projection plan, file content | storage placement and artifact record | storage write, DB mutation | no | Deliberately left in dispatcher for this bundle. |
| `ProjectProcessMockArtifactsAsync` key planning | `ProcessRunAutomationDispatchService.ArtifactProjection.cs` | projection-source-discovery | mock projection metadata | normalized source key | file read/storage/DB later | partial | Source-adapter key builder moved; full mock projection migration remains later. |
| `ProjectWorkspaceWrittenArtifactsAsync` key planning | `ProcessRunAutomationDispatchService.ArtifactProjection.cs` | projection-source-discovery | workspace write path and expectation | normalized source key | file read/storage/DB later | partial | Source-adapter key builder moved; full workspace projection migration remains later. |
| `ProjectResponseTextArtifactsAsync` key planning | `ProcessRunAutomationDispatchService.ArtifactProjection.cs` | projection-source-discovery | response path and execution id | normalized source key | storage/DB later | partial | Source-adapter key builder moved; full response projection migration remains later. |
| `IsProducerAllowedForMode` | `ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | validation-rule | expectation mode, producer kind | allow/reject decision | none | migrated | Delegates to `ProcessArtifactEvidenceValidationRules`. |
| `RequiresManagedEvidencePath` | `ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | validation-rule | expectation mode, producer kind | durable-path requirement | none | migrated | Delegates to `ProcessArtifactEvidenceValidationRules`. |
| `RequiresStoredArtifactContent` | `ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | validation-rule | expectation required flag, mode, producer, storage path | content-read requirement | none | migrated | Delegates to `ProcessArtifactEvidenceValidationRules`. |
| `ValidateArtifactExpectationForRecordedArtifacts` | `ProcessRunAutomationDispatchService.CompletionArtifactValidator.cs` | validation-rule | expected artifact, recorded artifacts, run ids | validation result | none | consumer only | Retains orchestration and consumes selected extracted rules through wrappers. |
| `ProjectExistingManagedArtifactFilesAsync`, `ProjectProviderNativeBrowserArtifactsAsync`, `EnsureDecisionArtifactsForCompletedStepAsync` | `ProcessRunAutomationDispatchService.ArtifactProjection.cs` | projection-source-discovery / storage-db-recording | mixed projection sources | artifact records | file/storage/DB | later | Not migrated in this bundle except shared key/lineage helpers. |

Categories:

- expectation-matching
- projection-source-discovery
- lineage-key-generation
- storage-db-recording
- validation-rule
- recovery-artifact
- completion-finalization
- utility
- cross-cutting
