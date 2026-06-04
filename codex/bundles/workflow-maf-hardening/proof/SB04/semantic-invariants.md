# SB04 Semantic Invariants

- Invariant ID: SB04-INV-001
- Source raw note: Plugin executors are part of the workflow runtime surface and must be deterministic, cancellable, permission-checked, and testable without live external services.
- Expected behavior: Executor descriptors carry typed capability, approval, and deterministic preview metadata, and approval-required executors fail before implementation code runs when approval is absent or denied.
- Disallowed shallow implementation: Adding display-only strings, relying on UI convention, or checking approval after side effects.
- Failing-first test: N/A - process hardening extended an existing executor contract; the negative approval cases are enforced by targeted tests.
- Passing test: `WorkflowExecutorPolicyObservabilityTests` approval/redaction tests and descriptor metadata tests passed.
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowExecutorModels.cs`, `repo://src/CanDoItAll.Plugins.Abstractions/PluginManifestContracts.cs`, `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExecutorContracts.cs`, `repo://src/plugins`.
- Production assertions: `WorkflowExecutorInvoker` calls `EnforceApprovalPolicyAsync` before building execution context and before executor implementation invocation.
- Red-team negative case: A denied approval message containing sensitive settings is redacted and the executor invocation count remains zero.
- Downstream dependency check: Gmail, Office365, Docker, built-in descriptors, runtime package executor registration, and plugin grants were covered by tests.
