# Requirement Traceability

| Requirement | Subbundle | Files | Proof |
| --- | --- | --- | --- |
| R001 | 01-manager-artifact-recovery | `ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs` | Targeted tests |
| R002 | 01-manager-artifact-recovery | `ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs` | Manager-agent routing assertion |
| R003 | 01-manager-artifact-recovery | `ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs` | Directive content assertion |
| R004 | 01-manager-artifact-recovery | `ProcessRunAutomationDispatchService.ArtifactProjection.cs` unchanged semantics | Existing and targeted tests |
| R005 | 01-manager-artifact-recovery | `ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs` | Blocked outcome assertion or directive test |
| R006 | 02-validation-proof | tests under `tests/CanDoItAll.Tests.Integration` | `dotnet test` proof |
