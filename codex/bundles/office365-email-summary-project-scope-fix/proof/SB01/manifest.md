# SB01 Proof Manifest

## Changed File Hashes

- DE5972835BDDA567AA71D282E08C5EED14CF5B79363B49A1B9C8C3C6CFA60D87 `repo://src/CanDoItAll.AgentFramework.Models/Conversations/ConversationModels.cs`
- 61F2E24DB51058930A8C050B63122C9151403B6B8AB08DC935FC973EB134DBC8 `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`
- 1E2E93BED634B502ED4F7B483EF6E81820551D82AB331776B309DB8D9D96C85E `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs`
- 25FEBFAA96A77528CE586DA800747BE9B28B3E3C928C1BC0AE231FBF108F3501 `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowLlmComponentInvoker.cs`
- C4A08C87335D7088D173CF9EC637CE35532CECACB14F727294F25F5EE7E64E5B `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryMafIntegration.cs`

## Proof Artifacts

- Passing transcript: `bundle://proof/SB01/transcripts/unit-tests.txt`
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.json`
- Failing-first: N/A process proof, original production log supplied the failing condition and the retained negative tests prove missing scope and recall outage still fail.

## Test Names

- Test name: `MafWorkflowLlmComponentInvokerPassesProjectScopeFromWorkflowPayload`
- Test name: `Maf_runtime_uses_context_workspace_scope_override_for_contributors`
- Test name: `Cognitive_memory_contributor_skips_empty_context_pack_for_process_automation`
- Test name: `Cognitive_memory_contributor_fails_process_automation_when_project_scope_is_missing`
- Test name: `Cognitive_memory_contributor_fails_process_automation_when_required_memory_is_unavailable`

## Proof Claim To Code Matrix

| Capability claim | Required production source proof | Required test proof | Required negative fixture | Result |
| --- | --- | --- | --- | --- |
| Workflow project scope reaches MAF context contributors | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowLlmComponentInvoker.cs`; `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs` | `bundle://proof/SB01/transcripts/unit-tests.txt` | Missing-scope test remains failing behavior | Passed |
| Empty memory does not block payload-only workflow | `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryMafIntegration.cs` | `bundle://proof/SB01/transcripts/unit-tests.txt` | Recall outage test remains failing behavior | Passed |
