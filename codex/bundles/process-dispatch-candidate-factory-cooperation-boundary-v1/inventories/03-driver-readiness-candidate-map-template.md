# Driver Readiness Candidate Map

Documentation-only. Do not create production driver APIs.

| Candidate signal | Current source | Future driver relevance | Current action |
| --- | --- | --- | --- |
| `AgentWorkspaceToolProfileKind.SoftwareDevelopment` | `ProcessDispatchCooperationMetadataResolver.ResolveWorkspaceToolProfile` | Future software-development helper driver selection | Document only; no production selection API |
| `AgentWorkspaceToolProfileKind.QualityValidation` | `ProcessDispatchCooperationMetadataResolver.ResolveWorkspaceToolProfile` | Future verification/proof helper drivers | Document only; no production selection API |
| `AgentWorkspaceToolProfileKind.BusinessAnalysis` | `ProcessDispatchCooperationMetadataResolver.ResolveWorkspaceToolProfile` | Future business/document/spreadsheet helper drivers | Document only; no production selection API |
| `AgentWorkspaceToolProfileKind.ArchitectureReview` / `SecurityReview` | `ProcessDispatchCooperationMetadataResolver.ResolveWorkspaceToolProfile` | Future specialist review helper drivers | Document only; no production selection API |
| `AgentProcessCooperationMode.ProcessArtifactHandoff` | `ProcessDispatchCandidateFactory` subprocess/workflow defaults and `ProcessDispatchCooperationMetadataResolver` direct-agent resolver | Future evidence input provider | Document only |
| `AgentProcessCooperationMode.MafLocalHandoff` / `A2ARemoteHandoff` / `Hybrid` | `ProcessDispatchCooperationMetadataResolver.ResolveCooperationMode` | Future manager-verifier and remote helper routing | Document only; no driver registry |
| `ProjectStructureAccessGrantedAndSaved` | `ProcessDispatchTechnicalAgentBindingCoordinator` | Future manager verification read access | Keep explicit side-effect boundary |
| `Subprocess` route | `ProcessDispatchCandidateFactory.CreateSubprocessCandidate` | Future nested-process helper boundary | Internal candidate construction only |
| `Workflow` route | `ProcessDispatchCandidateFactory.CreateWorkflowCandidate` | Future workflow bridge helper boundary | Internal candidate construction only |
| Direct-agent route facts | `ProcessDispatchDirectAgentCandidateFacts` and `ProcessDispatchCandidateFactory.CreateDirectAgentCandidate` | Future direct-agent driver facts | Internal candidate construction only |
