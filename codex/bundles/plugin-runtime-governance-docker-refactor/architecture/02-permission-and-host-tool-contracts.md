# Permission And Host Tool Contracts

## Contract Principles

- Runtime grants are separate from manifest declarations.
- Runtime checks are explicit and centralized.
- Denial is part of the feature. Every denied result must include plugin id, capability, reason code, and user-facing next action.
- Host execution is recipe-based. No plugin receives arbitrary command strings.
- Recipes are strongly typed. Argument models validate external protocol text before process execution.

## Proposed Contract Families

| Contract family | Purpose | Notes |
| --- | --- | --- |
| Plugin grant ids and records | Persist explicit approval state. | Include plugin id, connection id, capability kind, recipe id, scope, grant state, actor, timestamps, and concurrency token. |
| Grant evaluator | Produces allow/deny decisions for capability use. | Consumes manifest, installation state, connection state, grant records, and app policy. |
| Capability proxy factory | Builds `IPluginCapabilityContext` per invocation. | Undeclared or ungranted capabilities produce denied proxies. |
| Host-tool capability | Generic plugin-facing capability for reviewed local operations. | Accepts typed recipe requests, not shell strings. |
| Recipe registry | Maps typed recipe ids to implementations and policies. | Must support Docker and future generic recipes without changing plugin-core semantics. |
| Recipe receipt | Durable proof of command intent and result. | Include boundary descriptor, recipe id, args summary, env variable names, output caps, truncation, artifacts, and redacted errors. |

## Required Denial Reasons

- Plugin is not installed.
- Plugin is disabled.
- Manifest does not declare the requested capability.
- Capability grant is missing.
- Capability grant is denied or revoked.
- Required connection is missing.
- Required connection is unhealthy.
- Recipe is unknown or unavailable on this host.
- Recipe grant is missing.
- Recipe arguments violate policy.
- Host boundary cannot satisfy the requested risk class.

## Docker Recipe Boundary

- Docker list containers: read-only recipe; no image or container mutation.
- Docker pull image: network and disk mutation; requires registry/image policy and timeout.
- Docker start container: local process/container mutation; disallow privileged mode, host network, arbitrary mounts, raw entrypoint override, and secret env injection by default.
- Docker read logs: read-only but high-volume; requires tail, since, max characters, timeout, and artifact behavior.
- Docker CLI absence is a recipe-unavailable result, not a fallback to arbitrary PowerShell.

## PowerShell Boundary

- PowerShell is not a generic plugin capability.
- PowerShell can exist only as a reviewed host-tool recipe with script path allowlisting and typed arguments.
- Inline scripts from plugin settings are out of scope for this bundle.
- Host-tool recipe environments for plugins must start from a plugin-safe allowlist, not from the general workspace command environment.

## Workflow Boundary

- Workflow catalog entries for plugin executors must include source plugin id and availability diagnostics.
- Workflow validation must evaluate installation, enablement, connection, and grants.
- Workflow execution must use the same grant evaluator as validation.
- A grant state change after validation but before run must be rechecked at run time.

## Logging And Redaction

- Audit entries must include enough state to diagnose denial and execution, but not secrets.
- Environment-variable logs must list variable names only and must omit secret values.
- Docker log content must be treated as potentially sensitive. Redaction and truncation rules must apply before LLM input construction.
- LLM summaries must record source artifact references and truncation state.
