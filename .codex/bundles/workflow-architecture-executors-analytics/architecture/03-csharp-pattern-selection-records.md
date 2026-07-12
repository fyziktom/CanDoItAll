# C# Pattern Selection Records

## PSR-01: Executor Contribution Registry

- Forces: catalog and invoker require identical descriptor/implementation data; planned descriptors may be non-runnable; scoped DI lifetimes must remain correct.
- Decision: introduce one immutable contribution/registration contract consumed by both catalog and invoker, with an optional executor factory/instance for non-runnable planned entries.
- Rejected: reflection scanning plus a separate descriptor source, because drift is possible by construction.
- Rejected: service locator keyed by executor ID, because dependencies and failures become hidden.

## PSR-02: Ports And Adapters For Shared Operations

- Forces: runtime tools and workflow nodes have different policy envelopes but identical document/file/spreadsheet/image behavior.
- Decision: typed operation interfaces/results in an SDK-free inward boundary; ManagedCode, provider, filesystem, and command services are adapters; tools/executors are orchestration adapters.
- Rejected: workflow executor invoking an agent tool, because it couples workflow execution to tool transport, receipts, and conversation authorization.
- Rejected: duplicate executor implementation, because behavior and security patches drift.

## PSR-03: Application Launch Service

- Forces: five callers need consistent definition/version/status/backend/input policy while retaining origin-specific correlation.
- Decision: an application service accepts a typed launch intent and discriminated origin, then starts the runtime. Callers remain thin adapters.
- Rejected: expanding `WorkflowRunStartRequest` with more nullable GUID/string fields.

## PSR-04: Append Facts, Project Analytics

- Forces: multiple models, retries, partial failures, historical pricing, unknown usage, and incremental lifecycle.
- Decision: persist immutable correlated usage observations and lifecycle timestamps; query through a deterministic analytics projection.
- Rejected: parse event payload JSON in the UI or persist only a mutable aggregate, because detail/provenance and replay safety are lost.

## PSR-05: Strategy Registry For Settings Rendering

- Forces: built-in and plugin schemas need flexible UI; custom components are executable code and have trust/version constraints.
- Decision: descriptor selects Schema or a strongly typed renderer key; trusted composition registers renderer strategies and option sources; host validates contract/trust/version.
- Rejected: `Type.GetType(manifest.ComponentTypeName)` and arbitrary DynamicComponent activation.
- Rejected: executor-ID switch expressions in the editor.

## PSR-06: State Machine For Workflow Lifecycle

- Forces: accepted/running/waiting/terminal transitions, cancellation, external responses, crashes, and backend capability differences.
- Decision: explicit transition methods with persisted pre-execution and terminal states; backend capability controls supported transitions.
- Rejected: treating response submission as automatic completion or silently falling back to InProcess.

## PSR-07: Process Workflow Driver Adapter

- Forces: process assignments must select one workflow, recover a previously launched child after retry/restart, preserve process identity, and translate a workflow output into the existing typed process outcome contract.
- Decision: add an SDK-free `ProcessWorkflowExecutorBinding` value contract and one top-level Modules.Processes adapter over `IWorkflowLaunchService` plus `IWorkflowRuntimeManager`; the existing process execution adapter delegates only when executor kind is Workflow.
- Rejected: extend the unused `IWorkflowProcessExecutorBridge`, because it directly calls runtime start, cannot resolve/catalog-validate/recover, and duplicates launch policy.
- Rejected: encode workflow and version in `ExecutorId`, because composite strings cannot be validated or migrated safely.
- Rejected: make the broad process adapter own workflow catalog/event parsing, because isolated recovery/output tests would require constructing the full agent runtime integration.
- Dependency direction: Processes.Contracts/Runtime/Persistence own only GUID-based binding data; Modules.Processes references workflow abstractions and models at the outer integration boundary.
- Unit-test seam: instantiate the driver directly with fake launch/runtime services and the existing process result converter; resolver and persistence remain independently testable.
