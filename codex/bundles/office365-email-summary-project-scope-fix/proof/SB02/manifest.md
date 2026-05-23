# SB02 Proof Manifest

## Changed File Hashes

- 25FEBFAA96A77528CE586DA800747BE9B28B3E3C928C1BC0AE231FBF108F3501 `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowLlmComponentInvoker.cs`
- C4A08C87335D7088D173CF9EC637CE35532CECACB14F727294F25F5EE7E64E5B `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryMafIntegration.cs`

## Proof Artifacts

- Passing transcript: `bundle://proof/SB02/transcripts/integration-test.txt`
- Passing transcript: `bundle://proof/SB02/transcripts/live-office365-api-run.txt`
- Semantic positive proof: `bundle://proof/SB02/transcripts/live-office365-asset-proof.txt`
- Failing-first transcript: `bundle://proof/SB02/transcripts/live-office365-pre-fix-failure.txt`
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`
- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.json`

## Test Names

- Test name: `ProjectStructureAgentApi_llm_workflow_uses_project_scope_and_creates_markdown_asset_under_workflow_node`

## Proof Claim To Code Matrix

| Capability claim | Required production source proof | Required test proof | Required negative fixture | Result |
| --- | --- | --- | --- | --- |
| Office365 workflow creates project markdown asset | `repo://Templates/Workflows/workflows/default-workflows.yaml`; `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs` | `bundle://proof/SB02/transcripts/live-office365-api-run.txt` | `bundle://proof/SB02/transcripts/live-office365-pre-fix-failure.txt` | Passed |
| Created asset is under workflow node | `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureWorkflowNodeService.cs` | `bundle://proof/SB02/transcripts/live-office365-asset-proof.txt` | Integration development exposed lease mismatch when runContext was not preserved | Passed |
