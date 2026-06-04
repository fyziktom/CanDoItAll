# SB08 Semantic Invariants

## Invariant SB08_INV_001

- Invariant ID: `SB08_INV_001`
- Source raw note: "Create minimal `CanDoItAll.Processes.Contracts` or `CanDoItAll.Processes.Abstractions` if justified by SB03."
- Expected behavior: A solution-registered neutral process contracts project exists and contains only stable execution request/source/policy snapshots needed by the current execution boundary.
- Disallowed shallow implementation: Adding a project name while leaving contracts coupled to EF, Razor, AgentFramework, Workbench, product modules, or unrelated Process Core concepts.
- Failing-first test: `bundle://proof/SB08/transcripts/contracts-project-absent.failing-first.txt` shows the contracts project did not exist in `HEAD` before SB08.
- Passing test: `bundle://proof/SB08/transcripts/unit-architecture-tests.rerun.txt`; `bundle://proof/SB08/transcripts/contracts-foundation-source-scan.txt`.
- Changed source files: `repo://src/CanDoItAll.Processes.Contracts/CanDoItAll.Processes.Contracts.csproj`; `repo://src/CanDoItAll.Processes.Contracts/Automation/ProcessAutomationExecutionContracts.cs`; `repo://src/CanDoItAll.Processes.Contracts/README.md`; `repo://CanDoItAll.slnx`.
- Production assertions: `bundle://proof/SB08/source-assertions/contracts-foundation.txt`.
- Red-team negative case: Adding a package/project reference or forbidden framework/module namespace to the contracts project fails `Process_contracts_project_is_solution_registered_and_stays_neutral` and the neutrality scans.
- Downstream dependency check: SB09 can consume a stable process-owned execution request/source/policy shape without depending on AgentFramework DTOs directly.

## Invariant SB08_INV_002

- Invariant ID: `SB08_INV_002`
- Source raw note: "Add only stable identity/source/policy snapshot records needed by the execution boundary."
- Expected behavior: Dispatcher execution start builds a process-owned `ProcessAutomationExecutionRequest`, and the facade explicitly maps that neutral request into the existing AgentFramework `ExecutionRunRequest`.
- Disallowed shallow implementation: Defining unused records while the dispatcher still constructs AgentFramework execution requests at the call site, or silently defaulting missing source/policy fields.
- Failing-first test: `bundle://proof/SB08/transcripts/contracts-project-absent.failing-first.txt` proves no neutral request contract was available before SB08.
- Passing test: `bundle://proof/SB08/transcripts/integration-execution-client-tests.txt`; `bundle://proof/SB08/transcripts/dispatcher-neutral-request-scan.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAutomationExecutionClient.cs`; `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`; `repo://src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`; `repo://tests/CanDoItAll.Tests.Integration/ProcessAutomationExecutionClientTests.cs`.
- Production assertions: `bundle://proof/SB08/source-assertions/contracts-foundation.txt`.
- Red-team negative case: Removing explicit mapping for source, policy, structured output, or auto-approval fails `ProcessAutomationExecutionClientTests`.
- Downstream dependency check: SB09 lineage hardening can build on process-owned invocation snapshots while preserving current execution behavior.

## Invariant SB08_INV_003

- Invariant ID: `SB08_INV_003`
- Source raw note: "Do not move EF entities or UI view models" and "Do not create driver packs."
- Expected behavior: SB08 does not start Process Core extraction, does not move EF/UI models, does not add driver packs, and produces no browser/mobile proof because no rendered UI changed.
- Disallowed shallow implementation: Hiding a broad extraction behind a contracts project, moving entities/view models prematurely, adding driver assemblies, or producing small/medium/mobile validation artifacts.
- Failing-first test: N/A - this invariant is a scope guard for SB08.
- Passing test: `bundle://proof/SB08/transcripts/no-core-driver-project-scan.txt`; `bundle://proof/SB08/transcripts/no-entity-viewmodel-contracts-scan.txt`; `bundle://proof/SB08/transcripts/anti-stub-audit.txt`.
- Changed source files: No EF entity, UI component, view model, Process Core, or driver-pack files changed for SB08.
- Production assertions: `bundle://proof/SB08/source-assertions/contracts-foundation.txt`.
- Red-team negative case: Adding `CanDoItAll.Processes.Core`, a driver project, an EF entity, a Razor component, or a view model to the contracts project fails the SB08 architecture tests and scans.
- Downstream dependency check: SB10 can perform the next consistency checkpoint knowing SB08 stayed within the minimal contracts foundation scope.
