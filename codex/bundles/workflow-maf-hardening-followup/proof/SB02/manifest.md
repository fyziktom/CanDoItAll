# SB02 proof manifest

- Subbundle: `SB02`
- Status: `Completed`
- Owned requirements: R2, R3, R11
- Raw notes: HITL and approval-required executor flows must be execution-position aware, persist redacted request state, and avoid live external effects in proof.
- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`

## Changed File Manifest

| File | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowRuntimeManager.cs` | `72326de89c7ad02a0374c50bc10c85b3376e469538fad521d48f115774b70734` | `78bd808534769744d82094e43f1e3d1076309bc2d75bd326688fec5e0b4fe85a` |
| `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExternalRequestRuntime.cs` | `N/A` | `e081d2c2d062e1cec0829fe4b445b420860d3586207135d212c306adf86cdfbd` |
| `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs` | `25494b334d8ccf99f5dc61c413cee8b2aaf1cd2a593120333605e86acbd59441` | `96b653674188cf7cb29816e46bb012c49be12c31ec53ecbec1d07b13875742f2` |
| `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs` | `b9816a124b44ea5e7dfcd4f950d204217115faf2b389e178bced2a7aad110670` | `71d2a0c7e941c626a65ffc20cbd9d23f1a5b746abad859c3d7af167e1318e14d` |
| `repo://src/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs` | `7d43ba73d3842ee62dcc2815768d8f522357a459b4390a72374203630a4bf5c2` | `94ef579acd43813ecfd7b615765785f492e0040080a8e736eea51d55d8bfaa86` |
| `repo://src/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs` | `3109526e4813ffcad02c5a75e96e7528c1024ed61ea242011a29a6616e88c614` | `571a5a438495146da0884ca2a4395407feaf25e4fcc5e5d78b514c32765095d6` |
| `repo://tests/CanDoItAll.Tests.Unit/WorkflowFoundationTests.cs` | `29d0e9423e82b492b757b0ef2030af6cf2d58588b0d7ea570e24e2eab0cd37e1` | `bdd78f1dc3fb972bf7cc7b6f3646cbc13d3fcf0d19c0cc1513cfacf7f1586a19` |
| `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorPolicyObservabilityTests.cs` | `9856819b495dc3cc23e7f553fc45bffe21b380b95577af6a96e5ee34a1d22eeb` | `6422b459ce0be164ec120b804dbd6ac1d9ee363e2205ec2f7d8abf6ab26f0620` |
| `repo://tests/CanDoItAll.Tests.Integration/WorkflowApiIntegrationTests.cs` | `feab3a744263cd5c85b2cedc3393fbe62aab54c504f77400fa7d04860c788355` | `d61d175b96022e60d4baf19a3af287a803cc1ec522a4f45f791808f94e8fcfc8` |

Hash transcript: `bundle://proof/SB02/transcripts/changed-file-hashes.txt`

## Command Transcripts

- Failing-first HITL route proof: `bundle://proof/SB02/transcripts/failing-first-hitl-route-tests.txt`
- Interim failed implementation proof before capture-scope handling: `bundle://proof/SB02/transcripts/unit-hitl-approval-after-implementation.txt`
- Passing capture-scope proof: `bundle://proof/SB02/transcripts/unit-hitl-approval-after-capture-scope.txt`
- Passing unit proof after approval response semantics: `bundle://proof/SB02/transcripts/unit-hitl-approval-after-response-semantics.txt`
- Passing API integration proof: `bundle://proof/SB02/transcripts/integration-workflow-api-hitl-approval-after-implementation.txt`
- Passing component smoke proof: `bundle://proof/SB02/transcripts/component-workflows-page-smoke-after-hitl-approval.txt`
- Mistyped build command proof: `bundle://proof/SB02/transcripts/solution-build-after-hitl-approval.txt`
- Passing solution build proof: `bundle://proof/SB02/transcripts/solution-build-slnx-after-hitl-approval.txt`
- Semantic invariant index: `bundle://proof/SB02/transcripts/semantic-invariant-evidence.txt`

## Source Assertions

- Source-level assertion transcript: `bundle://proof/SB02/transcripts/source-assertions-hitl-approval.txt`
- Runtime manager no longer graph-scans for `HumanInput`: `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowRuntimeManager.cs`
- Reached human-input nodes create pending requests in the MAF workflow compiler: `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs`
- MAF backend captures external request state even when MAF turns the node throw into a failed run: `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`
- Product approval gate and redacted request payloads: `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExternalRequestRuntime.cs`
- Approval gate registration: `repo://src/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs` and `repo://src/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`

## Anti-Stub Audit

- Anti-stub transcript: `bundle://proof/SB02/transcripts/anti-stub-audit-hitl-approval.txt`
- Live external effects were not executed. Docker/Gmail/Office365 proof remains limited to capability flags and fake unit executors.

## Downstream Smoke Proof

- Workflow API smoke: `bundle://proof/SB02/transcripts/integration-workflow-api-hitl-approval-after-implementation.txt`
- Workflow component smoke: `bundle://proof/SB02/transcripts/component-workflows-page-smoke-after-hitl-approval.txt`
- Solution build: `bundle://proof/SB02/transcripts/solution-build-slnx-after-hitl-approval.txt`

## Known Residuals

- `dotnet build CanDoItAll.slnx --no-restore` still reports existing EF Core Relational `MSB3277` version-conflict warnings; it exits successfully with zero errors.
- Full workflow resume after approval response remains deferred to SB04 checkpoint/resume work; SB02 establishes durable pending request/decision semantics and direct approved/denied executor gate behavior.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| External request records | `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExternalRequestRuntime.cs` | `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowRuntimeManager.cs` | `bundle://proof/SB02/transcripts/unit-hitl-approval-after-response-semantics.txt` | `bundle://proof/SB02/transcripts/failing-first-hitl-route-tests.txt`; `bundle://proof/SB02/transcripts/unit-hitl-approval-after-response-semantics.txt` |
| Approval decisions | `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExternalRequestRuntime.cs` | `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExecutorContracts.cs` | `bundle://proof/SB02/transcripts/unit-hitl-approval-after-response-semantics.txt` | `bundle://proof/SB02/transcripts/unit-hitl-approval-after-response-semantics.txt` |
