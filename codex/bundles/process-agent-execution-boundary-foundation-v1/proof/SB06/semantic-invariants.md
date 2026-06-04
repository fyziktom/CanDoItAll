# SB06 Semantic Invariants

## Invariant SB06_INV_001

- Invariant ID: `SB06_INV_001`
- Source raw note: "Move direct execution start/detail/adoption/recovery calls from dispatcher execution partials behind the facade."
- Expected behavior: Dispatcher partials no longer call `workspaceService.*`; execution start, readback, adoption, recovery, costing, grounding, and provider repair operations route through `IProcessAutomationExecutionClient`.
- Disallowed shallow implementation: Migrating only the happy-path `ExecuteRunAsync` call while leaving detail reads, run listings, provider repair, or artifact recovery directly coupled to `IAgentFrameworkWorkspaceService`.
- Failing-first test: `bundle://proof/SB06/transcripts/dispatcher-direct-call-baseline.failing-first.txt` records the direct workspace-service calls present before SB06 migration.
- Passing test: `bundle://proof/SB06/transcripts/dispatcher-migration-architecture-tests.txt`; test name `ProcessAgentExecutionBoundaryArchitectureTests`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs`; `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`; `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs`; `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Costing.cs`; `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`; `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Grounding.cs`; `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs`; `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`.
- Production assertions: `bundle://proof/SB06/source-assertions/dispatcher-execution-client-migration.md`.
- Red-team negative case: Reintroducing `workspaceService.` or `IAgentFrameworkWorkspaceService workspaceService` in dispatcher partials fails `Dispatcher_execution_path_uses_process_owned_execution_client_after_migration`.
- Downstream dependency check: SB07 can now measure coupling reduction against the SB05 direct-call baseline.

## Invariant SB06_INV_002

- Invariant ID: `SB06_INV_002`
- Source raw note: "Preserve exception handling semantics for `AgentChatRunFailedException` and `AgentRunFailedException`; keep structured output/finalizer policy behavior unchanged."
- Expected behavior: SB06 changes the execution dependency receiver only; failed-run inspection, structured output contract, finalizer mode, retry policy, provider recovery, and adoption logic keep the existing control flow.
- Disallowed shallow implementation: Adding a wrapper that swallows exceptions, starts alternate retries, changes finalizer policy, or bypasses failed-run detail recovery.
- Failing-first test: `bundle://proof/SB06/transcripts/dispatcher-direct-call-baseline.failing-first.txt` shows the old direct dependency path that this migration removes.
- Passing test: `bundle://proof/SB06/transcripts/process-automation-execution-client-tests-after-migration.txt`; test name `ProcessAutomationExecutionClientTests`.
- Changed source files: Dispatcher partials listed under `SB06_INV_001`; hashes are recorded in `bundle://proof/SB06/transcripts/hashes.txt`.
- Production assertions: `bundle://proof/SB06/source-assertions/dispatcher-execution-client-migration.md`.
- Red-team negative case: Removing failed-run detail readback or changing the `ExecutionInvocationPolicy` block would be visible in the SB06 diff and violate the source assertion.
- Downstream dependency check: SB07 must confirm the reduced coupling without treating temporary AgentFramework DTO usage as a final Process Core contract.

## Invariant SB06_INV_003

- Invariant ID: `SB06_INV_003`
- Source raw note: "Do not reintroduce MAF product-tool dependencies" and "Do not run small, medium, or mobile UI validation."
- Expected behavior: MAF/Tooling product-reference scans remain clean for source/project files, and browser validation remains N/A because no UI changed.
- Disallowed shallow implementation: Moving coupling from dispatcher into MAF/Tooling or producing unrelated viewport proof.
- Failing-first test: N/A - SB06 does not change MAF/Tooling or rendered UI.
- Passing test: `bundle://proof/SB06/transcripts/maf-product-dependency-scan.txt`; `bundle://proof/SB06/transcripts/dispatcher-migration-architecture-tests.txt`.
- Changed source files: No MAF/Tooling source files changed in SB06.
- Production assertions: `bundle://proof/SB06/source-assertions/dispatcher-execution-client-migration.md`.
- Red-team negative case: A `CanDoItAll.Modules.Processes`, `CanDoItAll.Modules.Projects`, or `CanDoItAll.Modules.Workbench` source/project reference in MAF/Tooling would fail the dependency scan.
- Downstream dependency check: SB07 can proceed only with this product-dependency scan clean.
