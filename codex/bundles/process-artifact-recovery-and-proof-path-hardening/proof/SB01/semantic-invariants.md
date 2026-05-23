# SB01 Semantic Invariants

- Invariant ID: `INV-SB01-001`
- Source raw note: `N001`
- Expected behavior: current-run scoped product output reads satisfy implementation proof.
- Disallowed shallow implementation: accepting markdown summaries or unrelated managed roots as product proof.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first-live-db.txt`
- Passing test: `bundle://proof/SB01/transcripts/targeted-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ImplementationProof.cs`
- Production assertions: current-attempt tool receipts are scanned for managed product output paths.
- Red-team negative case: markdown-only artifact reads still fail proof.
- Downstream dependency check: `SB02` can rely on corrected proof classification.

- Invariant ID: `INV-SB01-002`
- Source raw note: `N001`
- Expected behavior: dotnet stdout and stderr are not browser console artifacts.
- Disallowed shallow implementation: treating generic process text artifacts as browser evidence.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first-live-db.txt`
- Passing test: `bundle://proof/SB01/transcripts/targeted-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs`
- Production assertions: browser refs require browser tool output or a scoped browser evidence reference.
- Red-team negative case: dotnet stdout result-summary evidence ref remains missing browser console proof.
- Downstream dependency check: QA/browser proof hardening remains meaningful.

| Invariant ID | Source raw note | Expected behavior | Disallowed shallow implementation | Failing-first test | Passing test | Changed source files | Production assertions | Red-team negative case | Downstream dependency check |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `INV-SB01-001` | `N001` | Product source or project reads under the scoped current-run output product root satisfy implementation proof. | Accepting markdown summaries, checklists, or unrelated managed roots as implementation proof. | `bundle://proof/SB01/transcripts/failing-first-live-db.txt` | `ResolveMissingConcreteImplementationProofSummary_accepts_source_read_under_scoped_current_run_output_root` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ImplementationProof.cs` | Current-attempt tool receipts are scanned for managed product output paths. | Managed evidence roots without product source still fail proof. | `SB02` can trust source proof before routing downstream recovery. |
| `INV-SB01-002` | `N001` | Dotnet stdout and stderr are not browser console artifacts. | Treating any text file in a process run folder as browser evidence. | `bundle://proof/SB01/transcripts/failing-first-live-db.txt` | `ResolveMissingRequiredArtifactSummary_rejects_dotnet_stdout_evidence_ref_as_browser_console_output` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs` | Browser refs require browser tool output or a scoped browser evidence reference. | Dotnet stdout result-summary evidence ref remains missing browser console proof. | QA/browser proof hardening remains meaningful. |

## Production Behavior Artifact Matrix

| Invariant | Producer | Consumer | Negative case |
| --- | --- | --- | --- |
| `INV-SB01-001` | `workspace_read_file` receipt | Implementation proof validator | Markdown-only artifact reads still fail implementation proof |
| `INV-SB01-002` | Result-summary `evidenceRefs` | Browser evidence artifact validator | Dotnet stdout path remains missing browser console artifact |
