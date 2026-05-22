# Requirement Traceability

| Requirement | Code | Tests | Proof |
| --- | --- | --- | --- |
| `R001` | `ProcessRunAutomationDispatchService.ImplementationProof.cs` | `ResolveMissingConcreteImplementationProofSummary_accepts_source_read_under_scoped_current_run_output_root` | `proof/SB01/transcripts/targeted-tests.txt` |
| `R002` | `ProcessRunAutomationDispatchService.ArtifactValidation.cs`, `ProcessRunAutomationDispatchService.ToolValidation.cs` | `ResolveMissingRequiredArtifactSummary_rejects_dotnet_stdout_evidence_ref_as_browser_console_output` | `proof/SB01/transcripts/targeted-tests.txt` |
| `R003` | Existing `ResolveMissingRequiredArtifactSummary` and recovery packet path | Existing dispatch tests | `proof/SB01/semantic-invariants.md` |
| `R004` | `ProcessRunAutomationDispatchService.Dispatch.cs`, `ProcessRunAutomationDispatchService.Models.cs`, artifact input resolution | `ShouldRetryIncompleteSuccessfulRun_does_not_retry_downstream_step_for_missing_upstream_artifact_block` | `proof/SB02/transcripts/targeted-tests.txt` |
| `R005` | `ProcessRuntimeProgressionPlanner.cs` | `ApplyTransitionConsequences_reactivates_blocked_dependent_after_upstream_artifact_materialization` | `proof/SB02/transcripts/targeted-tests.txt` |
| `R006` | All changed process runtime code | Full dispatch test class | `reviews/01-execution-report.md` |
