# SB03 Semantic Invariants

- Invariant ID: SB03-INV-001
- Source raw note: Runtime execution must have a clear native MAF adapter/compiler boundary instead of a second independent workflow engine.
- Expected behavior: The in-process backend depends on `IWorkflowMafCompiler`, compiles canonical definitions into MAF workflows, and emits stable node-level runtime records during MAF execution.
- Disallowed shallow implementation: Keeping only repository-local graph simulation, bypassing the compiler interface, or recording runtime output without node identifiers.
- Failing-first test: N/A - process-level hardening of an existing runtime path; no production behavior was shipped without the targeted compiler/backend tests.
- Passing test: `WorkflowExecutorTests.MafCompilerInvokesExecutorNodeThroughInvoker`, route semantics tests, and runtime backend rejection tests passed.
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs`, `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`, `repo://src/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs`, `repo://src/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`.
- Production assertions: `MafInProcessWorkflowExecutionBackend` uses `IWorkflowMafCompiler`, wraps MAF execution with `WorkflowBackendProgressEventObserver`, and merges observed records with MAF outgoing events.
- Red-team negative case: Route tests prove false and unselected branches are not executed.
- Downstream dependency check: DI registrations in hosting and module services expose the compiler interface to runtime consumers.
