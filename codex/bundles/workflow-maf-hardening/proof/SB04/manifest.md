# SB04 Proof Manifest

## Scope

Plugin executor contract and sandbox hardening.

## Changed File Hashes

- `5b397f79ebbf2b090f28fd77e273a412cfb4a6d884851f46722312fbebf95d97` `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowExecutorModels.cs`
- `731652ac6ab63fd96f0484baa1408dd62c21d0982e43a27079319c6b42d24d17` `repo://src/CanDoItAll.Plugins.Abstractions/PluginManifestContracts.cs`
- `36d95514f45e1298d655f4f2428509840ab3179cd4b98a5909e75010ceb97a04` `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExecutorContracts.cs`
- `1754895adc74cd81ad760cd1456f0240d1f6fc57d69c5dd50d4f1566a3e2854d` `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/BuiltInWorkflowExecutorDescriptors.cs`
- `98203d39694c117afa2c8b9df335378f44974b49180d84277d6cf2f9add0fda7` `repo://src/plugins/CanDoItAll.Plugin.Gmail/GmailWorkflowExecutor.cs`
- `55a947cc8d5eeae9b26d99972b388c409177cf67a081a34e25dd609233a06957` `repo://src/plugins/CanDoItAll.Plugin.Gmail/GmailBundledPlugin.cs`
- `ad41b429a607275be585876b20b35d86f01d97d9daff8a2ed57df3ae1e818d52` `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs`
- `95c476929d772f661f14dbe859e173e944709985b23f531f7867a6ad38e648fc` `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365BundledPlugin.cs`
- `3b80064afb917ab628d4fcb5c71828959560531431bcb2c3fbb8de3e494f472a` `repo://src/plugins/CanDoItAll.Plugin.Docker/DockerWorkflowExecutors.cs`
- `9b8ca7f03f1dc91e3ae3bee1668a4df23d27c0f34bef74b6f4b88be5af9dc9c9` `repo://src/plugins/CanDoItAll.Plugin.Docker/DockerBundledPlugin.cs`
- `c1f4c9b51481cc0d1b1d2be97855ee96fd215d405a2b6239810c0f70eac8280e` `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorPolicyObservabilityTests.cs`
- `896b9d9fdbd73c2bf9de7a684e822f16e640368796f13114d3d9bbd2598561b9` `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs`

## Evidence

- Semantic invariant contract: `bundle://proof/SB04/semantic-invariants.md`
- Failing-first transcript: N/A - process hardening of an existing executor contract with targeted negative approval tests.
- Passing transcript: `bundle://proof/SB04/transcripts/proof-summary.txt`
- Anti-stub audit transcript: `bundle://proof/SB04/transcripts/proof-summary.txt`

## Cited Tests

- Test name: `CanDoItAll.Tests.Unit.WorkflowExecutorPolicyObservabilityTests.PluginPolicy_invoker_rejects_approval_required_executor_without_gate`
- Test name: `CanDoItAll.Tests.Unit.WorkflowExecutorPolicyObservabilityTests.PluginPolicy_invoker_rejects_denied_approval_before_execution`
- Test name: `CanDoItAll.Tests.Unit.WorkflowExecutorTests.BuiltInDescriptorsExposeSourceAvailabilityAndSchemaMetadata`
- Test name: `CanDoItAll.Tests.Unit.WorkflowExecutorTests.WorkflowExecutorDescriptorDeserializesLegacyJsonWithDefaultMetadata`

## Invariants

- Invariant ID: `SB04-INV-001`
