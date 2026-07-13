# SB06 Semantic Invariants

- Invariant ID: SB06-INV-DOTNET-ISOLATION
- Source raw note: Bundle required .NET lifecycle and setup behavior to leave generic adapter and receipt-writer logic.
- Expected behavior: .NET lifecycle facts are emitted by a module extractor and runtime-owned setup uses a generic step executor interface.
- Disallowed shallow implementation: Keeping `IsDotNetRuntimeLifecycleTool` in Core or keeping a .NET-specific adapter dependency.
- Failing-first test: N/A process/non-production exemption; source assertions catch both old symbols.
- Passing test: `DotNetSolutionSetupRuntimeExecutorTests` in `bundle://proof/SB06/transcripts/passing.txt`.
- Changed source files: `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/DotNetWorkspaceCommandReceiptLifecycleFactExtractor.cs`, `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/DotNetSolutionSetupRuntimeExecutor.cs`.
- Production assertions: Source audit and focused tests in `bundle://proof/SB06/transcripts/passing.txt`.
- Red-team negative case: Reintroducing `workspace_dotnet_run` lifecycle enrichment in `WorkspaceCommandReceiptWriter` fails architecture baseline tests.
- Downstream dependency check: CodeAnalytics snapshot `snap-20260709182007-390484e5` returned `cycles: []`.
