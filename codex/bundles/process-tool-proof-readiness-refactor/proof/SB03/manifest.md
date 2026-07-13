# SB03 Proof Manifest

## Changed File Hashes

- `20a19e40cdefd0dc98e2423532f3d80893f76e27a163253f85d69a6600034a78` `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionReceipts.cs`
- `39372a0bb941333a750df9e90587f423bf22edf51faa6c2ed32cee74f0cb0858` `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ResultConversion.cs`
- `53b74dec40314e3f66bbad39451b8df610202c908128bf6a143fc13c005bde19` `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRequiredToolReceiptGate.cs`
- `cb6e5b350c560bf8c5593eb97895faba4d38a05bf82fe898092cff8e88ee04cd` `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs`

## Proof Artifacts

- Passing transcript: `bundle://proof/SB03/transcripts/proof-transcript.log`
- Raw focused test log: `bundle://proof/SB03/transcripts/focused-tests.log`
- Anti-stub audit transcript: `bundle://proof/SB03/transcripts/proof-transcript.log`
- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`
- Failing-first: N/A - process manager recovery is tested through an adversarial negative blocked-output case that must produce a retry diagnostic.

## Test Names

- Test name: `Blocked_step_with_missing_process_receipt_gets_manager_retry_diagnostic`

