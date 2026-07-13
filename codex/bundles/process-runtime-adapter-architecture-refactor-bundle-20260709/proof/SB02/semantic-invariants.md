# SB02 Semantic Invariants

- Invariant ID: SB02-INV-CONTRACT-SEAMS
- Source raw note: Bundle required contracts and boundary seams before responsibility movement.
- Expected behavior: Adapter consumes generic runtime-owned step executors and receipt writer consumes lifecycle fact extractors.
- Disallowed shallow implementation: Renaming a .NET-specific interface while leaving direct adapter ownership.
- Failing-first test: N/A process/non-production exemption; source assertions catch the old symbols.
- Passing test: `ProcessRuntimeIntegrationAdapterTests.ExecuteAsync_uses_runtime_owned_dotnet_setup_executor_before_agent_execution` in `bundle://proof/SB02/transcripts/passing.txt`.
- Changed source files: `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeOwnedStepExecutor.cs`, `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandReceiptLifecycleFacts.cs`.
- Production assertions: DI wiring and source assertion proof in `bundle://proof/SB02/transcripts/passing.txt`.
- Red-team negative case: Reintroducing the old .NET setup executor name fails `ProcessRuntimeArchitectureBaselineTests`.
- Downstream dependency check: CodeAnalytics snapshot `snap-20260709182007-390484e5` returned `cycles: []`.
