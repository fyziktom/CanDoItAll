# Current Problem Analysis

## Problem Statement

The latest repairs moved some behavior in the correct functional direction, but they reinforced the wrong architecture shape. The process adapter now has more behavior hidden inside partial files. The generic runtime and MAF core also contain .NET-specific decisions. This makes the process system harder to reason about, harder to test, and more likely to reintroduce the same escalation and repair-loop failures.

The architecture problem is not simply that `AgentFrameworkProcessExecutionAdapter.cs` is large. The deeper problem is that many independent responsibilities still share the same type, state, helper methods, issue types, and result-conversion code path.

## Responsibility Mixing In The Adapter

The adapter currently owns or participates in:

- Assignment loading.
- Subprocess bridge resolution.
- Subprocess launch coordination.
- Executor binding validation.
- Agent readiness validation.
- Runtime tool preflight.
- Runtime-owned .NET setup execution.
- MAF execution run creation.
- Structured output deserialization.
- Managed artifact materialization.
- Managed artifact acceptance and append logic.
- Grounded evidence validation.
- Product mutation evidence.
- Product completion gates.
- Required tool receipt matching.
- Branch-routed completion issue conversion.
- Runtime gate findings artifact writing.
- Produced artifact content hashing.
- Result conversion to `ProcessExecutionAdapterResult`.
- Recovery issue creation and retry/manager issue summaries.

This violates single responsibility and blocks isolated tests. A test for required receipt matching or branch routing should not need to construct the whole adapter or an agent workspace service.

## Partial Class Finding

The partial files are not acceptable as final architecture. They group behavior by filename, but still share one private state surface and one type identity. That makes each extracted area dependent on every other private helper and discourages explicit contracts.

This bundle treats the existing partial files as temporary migration debt. Implementation must move behavior into top-level services and delete partial files as responsibilities leave the adapter.

## Domain Leak Finding

The process runtime and dispatcher must stay generic. Current leaks include:

- `AgentFrameworkProcessExecutionAdapter` directly injecting and invoking `IDotNetSolutionSetupRuntimeExecutor`.
- `AgentFrameworkProcessExecutionAdapter.DotNetSetupRuntime.cs` containing the .NET setup flow inside the adapter partial cluster.
- `WorkspaceCommandReceiptWriter.IsDotNetRuntimeLifecycleTool` special-casing `workspace_dotnet_run` and `workspace_dotnet_stop`.
- Product completion receipt logic in the adapter recognizing `workspace_dotnet_new`, `workspace_pwsh_run_script`, and dotnet template requirements.
- Domain-specific step keys such as `create-dotnet-project`, `add-test-project`, and `repair-solution-setup` appearing in runtime integration code.

Some .NET-aware code is valid in the system. The problem is ownership. .NET tool-plan and lifecycle decisions belong in a domain driver, driver policy, or tool/lifecycle classifier implementation. The generic adapter/runtime should receive typed results from those seams.

## Why The Last Repairs Did Not Solve The Class Of Failure

The previous changes improved detection and instructions:

- Empty product artifacts can now be detected.
- Some required receipts are checked.
- Runtime-owned .NET setup exists.
- Prompt and preflight rules became more explicit.

They did not solve the architecture problem:

- Deterministic repair is still not cleanly routed through driver-owned policy.
- Completion and receipt gates are still module/adapter-local.
- Safe/idempotent failures can still become manager escalation too early.
- Parent subprocess root-cause propagation is not a separate testable service.
- More domain-specific code was added near the adapter instead of isolated behind driver contracts.
- The partial-class cluster grew rather than shrinking.

## Target Correction

The implementation must turn the adapter into an orchestration shell. Each behavior with its own reason to change must become a top-level service, policy, evaluator, or driver component with direct unit tests. Domain-specific .NET/software-delivery behavior must be plugged into generic runtime through contracts, not known by generic code.

