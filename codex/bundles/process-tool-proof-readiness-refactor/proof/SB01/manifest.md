# SB01 Proof Manifest

## Changed File Hashes

- `3a92f5fd3f71a0fa80a2d6d8edab4c429cfbd8483947608d6ff94d0f089eb20b` `repo://src/Processes/CanDoItAll.Processes.Contracts/ProcessCapabilityScopeModels.cs`
- `92dc4545310a77befd33cfc9157b70f20b6381c8e079872405a0248a2516b1e3` `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Runtime/AgentRuntimeCapabilityScopeModels.cs`
- `53b74dec40314e3f66bbad39451b8df610202c908128bf6a143fc13c005bde19` `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRequiredToolReceiptGate.cs`
- `a92c05cd56dfc7ecb2dddcea335b6a1f83cfd2b31a78675e2f2257c9e4c62f00` `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessCapabilityScopeTranslator.cs`
- `cb6e5b350c560bf8c5593eb97895faba4d38a05bf82fe898092cff8e88ee04cd` `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs`
- `2c4a50b64b74197d8433ac15bc1bfb58e5d20b48d60178d7c41caa4dca45c60b` `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationMetadataTests.cs`

## Proof Artifacts

- Passing transcript: `bundle://proof/SB01/transcripts/proof-transcript.log`
- Raw focused test log: `bundle://proof/SB01/transcripts/focused-tests.log`
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/proof-transcript.log`
- MAF boundary proof transcript: `bundle://proof/SB01/transcripts/maf-boundary-proof.log`
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`
- Failing-first: N/A - process behavior uses adversarial negative unit tests for missing and stale receipts instead of a separate failing transcript.

## Test Names

- Test name: `Completion_requires_process_capability_scope_tool_receipts`
- Test name: `Completion_accepts_process_capability_scope_current_run_tool_receipt`
- Test name: `Completion_rejects_stale_process_capability_scope_tool_receipt`
- Test name: `Scoped_capability_policy_is_attached_to_agent_runtime_options`

