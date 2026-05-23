# Requirement Traceability

| Requirement | Code | Tests | Proof |
| --- | --- | --- | --- |
| `R001` | `ProcessRunAutomationDispatchService.ImplementationProof.cs` | `ResolveMissingConcreteImplementationProofSummary_accepts_source_read_under_scoped_current_run_output_root` | `proof/SB01/transcripts/targeted-tests.txt` |
| `R002` | `ProcessRunAutomationDispatchService.ArtifactValidation.cs`, `ProcessRunAutomationDispatchService.ToolValidation.cs` | `ResolveMissingRequiredArtifactSummary_rejects_dotnet_stdout_evidence_ref_as_browser_console_output` | `proof/SB01/transcripts/targeted-tests.txt` |
| `R003` | Existing `ResolveMissingRequiredArtifactSummary` and recovery packet path | Existing dispatch tests | `proof/SB01/semantic-invariants.md` |
| `R004` | `ProcessRunAutomationDispatchService.Dispatch.cs`, `ProcessRunAutomationDispatchService.Models.cs`, artifact input resolution | `ShouldRetryIncompleteSuccessfulRun_does_not_retry_downstream_step_for_missing_upstream_artifact_block` | `proof/SB02/transcripts/targeted-tests.txt` |
| `R005` | `ProcessRuntimeProgressionPlanner.cs` | `ApplyTransitionConsequences_reactivates_blocked_dependent_after_upstream_artifact_materialization` | `proof/SB02/transcripts/targeted-tests.txt` |
| `R006` | All changed process runtime code | Full dispatch test class | `reviews/01-execution-report.md` |
| `R007` | `Templates/Processes/**` | Template-pack projection tests | `proof/SB03/manifest.md` |
| `R008` | `Templates/Processes/**` | Template-pack evidence contract tests | `proof/SB03/manifest.md` |
| `R009` | Agent/provider/runtime API records | HR/tool readiness audit | `proof/SB04/manifest.md` |
| `R010` | Project-structure API records | API transcript and backup manifest | `proof/SB05/manifest.md` |
| `R011` | Cognitive Memory settings API, runtime diagnostics | Runtime readiness transcript | `proof/SB05/manifest.md`, `proof/SB06/manifest.md` |
| `R012` | Process directives/artifacts and run summaries | Run-summary transcript | `proof/SB06/manifest.md` |
| `R013` | Generated output under `C:\programovani\dotnet-demo\output` and project-structure evidence | Browser validation transcript/screenshots | `proof/SB07/manifest.md` |
| `R014` | Run analysis and final review | Failure-classification record when applicable | `reviews/01-execution-report.md` |
