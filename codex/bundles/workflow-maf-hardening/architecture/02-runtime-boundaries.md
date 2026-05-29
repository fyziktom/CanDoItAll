# Runtime Boundaries

## Boundary 1: Definition authoring vs execution

Authoring sources:

- YAML templates under `Templates/Workflows`
- UI-created workflow definitions
- Managed seed definitions

Execution target:

- Native MAF workflow produced by a compiler/adapter from the canonical repository definition.

Rule: no page, seed service, or plugin may bypass validation and directly launch ad-hoc execution logic.

## Boundary 2: JSON persistence vs typed MAF messages

JSON is acceptable at storage/template boundaries. Inside runtime, JSON must be wrapped in explicit message types with metadata. Avoid method signatures such as `Task<object> ExecuteAsync(object input)` for plugin executors unless they are legacy adapters hidden behind validated typed wrappers.

## Boundary 3: Plugin discovery vs plugin execution

Plugin discovery may happen at startup or configuration time. Plugin execution must happen through the workflow executor registry and activation context.

Activation context must include:

- Run ID
- Node ID
- Tenant/user/security context
- Cancellation token
- Executor policy
- Artifact writer
- Telemetry/event writer
- Approval service
- Secret/credential access abstraction

## Boundary 4: Approval and external requests

Human approval and external request nodes must be modeled as workflow events and durable run states, not as transient UI booleans.

Approval-required operations include at least:

- Sending email or modifying external mail state
- Deleting or overwriting files
- Running Docker commands or shell-like operations
- Calling external APIs with side effects
- Accessing sensitive credentials or large external data exports

## Boundary 5: Test/live split

Each plugin executor must have:

- A deterministic fake connector mode for unit/integration tests.
- Optional live integration tests marked separately and skipped by default unless configured.
- No dependency on real Gmail/Office365/Docker credentials for closure proof.
