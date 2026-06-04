# SB06 semantic invariants

Status: Completed

## SB06-PLUGIN-GOVERNANCE

- Invariant ID: `SB06-PLUGIN-GOVERNANCE`
- Source raw note: R7 and R8 require deterministic plugin observer composition and manifest permission/capability validation.
- Expected behavior: plugin executor audit records reach plugin logs regardless of module registration order, and manifest validation rejects network, secret, host-command, external-write, and deterministic-mode mismatches.
- Disallowed shallow implementation: replacing the process-wide observer with a plugin observer, relying on DI order, or trusting plugin executor descriptors without manifest capability validation.
- Failing-first test: `bundle://proof/SB06/transcripts/failing-first-plugin-governance-tests.txt` and `bundle://proof/SB06/transcripts/failing-first-plugin-manifest-validation-tests.txt` failed before implementation.
- Passing test: `bundle://proof/SB06/transcripts/unit-plugin-manifest-validation-after-implementation.txt` and `bundle://proof/SB06/transcripts/integration-plugin-governance-after-implementation.txt` passed.
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExecutorObservability.cs`, `repo://src/CanDoItAll.Modules.Plugins/Catalog/PluginLogServices.cs`, `repo://src/CanDoItAll.Plugins.Abstractions/PluginManifestValidation.cs`, and tests listed in `bundle://proof/SB06/manifest.md`.
- Production assertions: `bundle://proof/SB06/transcripts/source-assertions-plugin-governance.txt` verifies observer composition, plugin sink registration, validation issues, permission flags, and tests.
- Red-team negative case: manifest tests cover missing capabilities, missing connection metadata, host command approval, external write approval, and deterministic-mode mismatch.
- Downstream dependency check: `bundle://proof/SB06/transcripts/integration-plugin-catalog-class-after-implementation.txt` and `bundle://proof/SB06/transcripts/build-after-sb06.txt` passed.

## Observer Composition

- `IWorkflowExecutorExecutionObserver` is the execution-time boundary and must remain order-independent.
- Plugin audit logging is an `IWorkflowExecutorExecutionAuditSink`; it must not replace or be replaced by the process-wide execution observer.
- The composite observer records to registered sinks in a deterministic order.
- Module registration order must not disable plugin executor audit persistence.

## Manifest Governance

- Workflow executor permission policies must be consistent with plugin manifest capabilities.
- Network or external data access requires `HttpClient` or `OAuth2` plugin capability.
- Secret-using executors require `SecretReference` or `OAuth2` capability plus secret or OAuth connection metadata.
- Host-command executors require `HostCommand` capability and `AlwaysRequired` approval.
- External-write executors require approval and must not declare `NotRequired`.
- Deterministic test mode must be declared consistently by both permission flags and executor metadata.

## Fake-Mode Boundary

- Required baseline proof for Gmail, Office365, and Docker uses preview simulation templates only.
- Fake-mode plugin tests must fail if the live executor implementation is invoked.
- Docker preview simulation must not execute host commands or shell processes.
- Live external-effect proof remains opt-in and outside the default test path.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Executor audit sink records | composite observer and plugin sink | plugin log store | Written after executor audit events regardless of registration order. | `bundle://proof/SB06/transcripts/integration-plugin-governance-after-implementation.txt` |
| Plugin manifest validation issues | plugin manifest validator | plugin catalog/install path | Emitted before invalid executor metadata is accepted. | `bundle://proof/SB06/transcripts/unit-plugin-manifest-validation-after-implementation.txt` |
