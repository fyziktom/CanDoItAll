# SB01 Proof Manifest

- Subbundle: `SB01`
- Proof type: dispatch grounding and external-target final-delivery prompt proof.
- Portable source references: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ProjectPaths.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`.
- Semantic invariant contract: `proof/SB01/semantic-invariants.md`.
- Passing command: `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName=CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.BuildProjectStructureGroundingSummary_includes_output_folder_from_top_level_architecture_branch_for_nested_delivery_target|FullyQualifiedName=CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.BuildExecutionPromptCore_requires_external_target_final_delivery_proof_when_grounded"`.
- Passing transcript: `bundle://proof/SB01/transcripts/passing.md`.
- Anti-stub transcript: `bundle://proof/SB01/transcripts/anti-stub.md`.
- Failing-first: N/A process prompt generation proof; adversarial negative proof is captured by the explicit semantic invariant and bounded-source test design.
- Changed-file SHA-256: `20BB8D799208903774D1CE164ACD8CE1D4EA61B9C2D111EF440B46B601B2A49F` for `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ProjectPaths.cs`.
- Changed-file SHA-256: `4DECD2E760817049167D88AF0F1F39C709476A9076180E1A968D12AB3C5074AE` for `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs`.
- Changed-file SHA-256: `DB9730D0160920ECA6AFB08E2E5BEB1B6CDE4DF3FE2DF44ED7438D4D9FCE820E` for `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`.
