# SB07 Proof Manifest

## Subbundle

- ID: SB07
- Title: Composition registration and runtime smoke
- Status: Completed
- Critical foundation: Yes
- Owned requirements: RQ-009, RQ-014
- Raw notes: "rozplest ty zavislosti"; "po mensich krocich"; "nesmi veci zjednodusit nebo neco vynechat"
- Semantic invariant contract: `bundle://proof/SB07/semantic-invariants.md`

## Changed Files With Hashes

| File | SHA-256 | Reason |
| --- | --- | --- |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeToolProviderCompositionIntegrationTests.cs` | `60819D2A0FC62F18E3F28A8227F44E9A3F593BD5F3972B1BB7A81854D1A8179E` | Adds real app-composition DI proof that the Processes provider is registered exactly once and exposes all 23 process tools. |
| Changed file hash transcript | `bundle://proof/SB07/source-assertions/changed-file-hashes.txt` | Full hash evidence for the touched SB07 test file. |

## Commands

| Command | Transcript path | Exit code | Purpose |
| --- | --- | ---: | --- |
| `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter ProcessRuntimeToolProviderComposition` | `bundle://proof/SB07/transcripts/runtime-tool-provider-composition-tests.txt` | 0 | Proves real `TestApplication` composition registers the Processes runtime tool provider and all 23 process tools. |
| `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter MafAgentRuntimeToolProviderComposition_zero_registered_providers_does_not_attach_process_tools` | `bundle://proof/SB07/transcripts/maf-zero-provider-tests.txt` | 0 | Proves MAF starts with no registered runtime providers and does not attach process tools. |
| `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter ProcessOutbox` | `bundle://proof/SB07/transcripts/process-outbox-tests.txt` | 0 | Proves durable process automation outbox smoke still passes. |
| `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ResolveCompletionStatus_allows_completion_when_required_step_tools_succeed|FullyQualifiedName~ResolveCompletionStatus_fails_when_required_step_tools_were_not_executed|FullyQualifiedName~ResolveSuccessfulWorkspaceFileMutationReceiptPaths_extracts_receipt_only_artifact_writes"` | `bundle://proof/SB07/transcripts/process-receipt-semantics-tests.txt` | 0 | Proves receipt-backed completion semantics still accept successful required tools, reject missing tools, and read mutation receipts. |
| `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter TransitionStepAsync_SB01_INV_001_allows_automation_completion_with_matching_execution_lineage_required_artifact` | `bundle://proof/SB07/transcripts/process-artifact-lineage-tests.txt` | 0 | Proves current-run automation artifact lineage still completes a governed process step. |
| `dotnet build CanDoItAll.slnx` | `bundle://proof/SB07/transcripts/solution-build.txt` | 0 | Proves the full solution builds after SB07 runtime-smoke test addition. |

## Validator Proof Citations

- Adversarial negative proof: N/A process/non-production preserved failing-first transcript; runtime composition, zero-provider, receipt, and lineage tests are the maintained regression proof.
- Passing transcript: `bundle://proof/SB07/transcripts/runtime-tool-provider-composition-tests.txt`.
- Anti-stub audit transcript: `bundle://proof/SB07/transcripts/anti-stub-audit.txt`.

## Source Assertions

| Assertion | Source path | Result |
| --- | --- | --- |
| Runtime composition source assertion passed. | `bundle://proof/SB07/source-assertions/runtime-composition-source-assertion.txt` | Processes module registers `ProcessAgentRuntimeToolProvider`; `TestApplicationBootstrap` loads runtime modules; SB07 test resolves `IAgentRuntimeToolProvider` from real DI and asserts all 23 tools. |
| Anti-stub audit passed. | `bundle://proof/SB07/source-assertions/anti-stub-audit.txt` | No TODO, `NotImplementedException`, pending, or stub markers in the SB07 integration test, provider registration file, or process provider file. |

## Semantic Adequacy Gate

| Label | Evidence |
| --- | --- |
| Raw note owned | SB07 proves the small-step dependency inversion works in real app composition without dropping process runtime behavior. |
| Shipped behavior | `TestApplication` composition registers one Processes provider; MAF still runs without providers; process outbox, tool-receipt, and artifact-lineage smoke tests pass. |
| Source proof | `bundle://proof/SB07/source-assertions/runtime-composition-source-assertion.txt`. |
| Test proof | `bundle://proof/SB07/transcripts/runtime-tool-provider-composition-tests.txt`, `bundle://proof/SB07/transcripts/maf-zero-provider-tests.txt`, `bundle://proof/SB07/transcripts/process-outbox-tests.txt`, `bundle://proof/SB07/transcripts/process-receipt-semantics-tests.txt`, `bundle://proof/SB07/transcripts/process-artifact-lineage-tests.txt`, and `bundle://proof/SB07/transcripts/solution-build.txt`. |
| Shallow-pass trap | A compile-only or unit-only check could miss missing real DI registration, zero-provider leaks, or process evidence regressions. SB07 includes real app composition plus process behavior smoke. |
| Adversarial negative proof | Missing process provider registration, missing expected tool name, process tool leakage with zero providers, missing required tool receipts, or stale/wrong lineage would fail the targeted tests. |
| Semantic positive proof | All 23 process tools resolve from the registered provider in real app composition; process automation outbox drains; successful tool receipts and current-run artifact lineage remain accepted. |
| Anti-stub audit | `bundle://proof/SB07/source-assertions/anti-stub-audit.txt`. |

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative test |
| --- | --- | --- | --- | --- |
| N/A | SB07 adds runtime/integration proof only; it introduces no persisted production state, signal, record, or event. | N/A | N/A | N/A |
