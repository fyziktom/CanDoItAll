# Workflow MAF Hardening

CanDoItAll persists workflow definitions in its domain model and executes them through a Microsoft Agent Framework adapter boundary. The domain model remains the canonical shape for storage, API, and UI editing; MAF is the runtime execution boundary.

## Authoring Rules

- Workflow template packs live under `Templates/Workflows/manifest.yaml` and referenced YAML workflow files.
- Template graphs are converted to canonical `WorkflowGraph` instances and semantically validated during template pack load.
- Workflow definitions are validated before persistence. Invalid nodes, edges, components, executor settings, or unavailable executors fail the save operation instead of being stored for a later publish-time failure.
- User-managed workflow definitions must not be overwritten by example seeding unless they carry the configured managed seed marker.

## Runtime Boundary

- `IWorkflowMafCompiler` is the adapter seam between canonical workflow definitions and native MAF workflow instances.
- `MafInProcessWorkflowExecutionBackend` is the preview/in-process backend and records node progress as workflow event records with stable node ids.
- Runtime policy is enforced before backend dispatch. A workflow that requires durable production execution and disables in-process previews cannot be started through the in-process backend.
- Durable production backends must be registered explicitly. Missing backends fail predictably with a runtime error.

## Executor Contract

Workflow executors must expose a `WorkflowExecutorDescriptor` with:

- stable executor id, display metadata, source, availability, input/result shapes, settings schema, and default execution policy;
- `PermissionPolicy` describing required capabilities and approval requirement;
- `DeterministicTestMode` describing whether preview or fake execution can prove behavior without live external services.

Executor descriptors default to no capabilities, no approval requirement, and no deterministic mode so legacy serialized descriptors remain safe and explicit.

## Approval Policy

`WorkflowExecutorInvoker` enforces `WorkflowExecutorPermissionPolicy` before executor implementation code runs.

- `NotRequired` executors run without an approval gate.
- `RequiredForExternalEffect` and `AlwaysRequired` executors require an `IWorkflowExecutorApprovalGate`.
- If no gate is registered, invocation fails before side effects.
- If approval is denied, invocation fails before side effects and denial text is redacted before it is included in the exception message.

Current bundled policy intent:

- Gmail and Office365 download executors read external data, use network/secrets, and support deterministic preview without approval.
- Gmail and Office365 mark-processed executors write external data and require approval for external effects.
- Docker workflow executors run host commands and always require approval.

## Testing And Troubleshooting

- Use template loader tests to prove every repository template remains loadable and semantically valid.
- Use compiler/backend tests to prove representative graphs compile and emit stable runtime events.
- Use plugin executor tests with fakes for success, failure, cancellation, redaction, approval denial, and artifact paths.
- Live Gmail, Office365, and Docker proof remains optional unless the required local services and secrets are configured; deterministic fake proof is the required baseline.
- A save response containing `Workflow definition save failed validation` means the definition was rejected before persistence. Fix the reported node, edge, component, or executor issue and retry the save.
