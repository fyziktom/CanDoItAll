# Capability Template Pack Design

## Root Manifest

`Templates/Capabilities/manifest.json` should declare:

- `schemaVersion`
- `packVersion`
- `managedSeedVersion`
- `capabilities` file list
- `policies` file list
- `compatibility` notes for preserved keys and runtime names

## Common Capability Fields

| Field | Required | Notes |
| --- | --- | --- |
| `key` | yes | Stable lower kebab-case catalog key. |
| `kind` | yes | Strong enum: `skill`, `tool`, `mcpServer`. |
| `stableIdKey` | yes | Stable seed ID source, for example `capabilities/workspace-dotnet-build`. |
| `displayName` | yes | UI text only. |
| `description` | yes | User/model-facing description. |
| `tags` | yes | Used for filtering and ownership. |
| `operationClassifications` | yes for tools/MCP tools, no for passive skills | Typed behavior categories used by policy, for example read, validation, mutation, script, browser, document, project-structure, or provider-native. |
| `defaultAssignments` | no | Agent/team template assignment guidance, if needed. |
| `configuration` | yes | Typed per-kind object. |
| `validation` | yes | Setup-test and schema validation expectations. |

## Tool Template Types

- `internal`: binds to a typed implementation key and method/service descriptor.
- `externalProcess`: invokes a configured executable/script with JSON input/output contract, timeout, working directory policy, and secret bindings.
- `externalHttp`: invokes an HTTP endpoint with method, URL template, headers from secret bindings, input/output schema, and timeout.
- `providerNative`: declares hosted provider-native tools without pretending they are local functions.

## MCP Template Types

- `internalHosted`: starts an in-process or app-owned MCP server with lifecycle ownership.
- `localStdio`: starts a local command through approved command policy and cleans it up.
- `remoteHttp`: connects to a streamable HTTP/SSE compatible server with header bindings.

## Skill Template Types

- `file`: references a checked-in skill root with `SKILL.md`.
- `inline`: embeds instructions/resources in template data or separate markdown files.
- `registered`: binds to a typed implementation descriptor, not an arbitrary stringly service type.

## Capability Access Policy Templates

Policy templates can appear under `Templates/Capabilities/policies`, agent/team templates, process definitions, process steps, workflow definitions, and workflow nodes. They must compile into the domain model described in `architecture/05-capability-access-policy.md`.

Normal template authors should use a bounded policy shape:

| Field | Required | Notes |
| --- | --- | --- |
| `defaultEffect` | no | `inherit`, `allowAssigned`, or `denyAll`. Compiled to typed defaults. |
| `rules` | yes when policy is present | Ordered for readability only; evaluator precedence is deterministic and not order-dependent. |
| `rules[].effect` | yes | Strong enum text such as `allow`, `deny`, or `require`. Deny wins over allow. |
| `rules[].scope` | yes | Strong enum text such as `agent`, `workflow`, `workflowNode`, `process`, `processStep`, or `runtimeOverride`. |
| `rules[].selector.kind` | yes | Bounded selector kind: `all`, `kind`, `capabilityKey`, `tag`, `operationClassification`, `runtimeToolName`, `mcpServerKey`, `mcpToolName`, or `implementationKey`. |
| `rules[].selector.value` | conditional | Parsed to typed value objects. `mcpToolName` must include server context to avoid ambiguity. |
| `rules[].reason` | yes for deny/require | User/agent-facing repair context. |

Policy rules restrict the already assigned/enabled candidate set. `allow` must never grant a capability that the agent did not already have. Invalid selector text, unknown capability keys, unsupported enum values, and ambiguous MCP tool names must fail validation before seed or runtime materialization.

Example:

```json
{
  "capabilityAccessPolicy": {
    "defaultEffect": "inherit",
    "rules": [
      {
        "effect": "deny",
        "scope": "processStep",
        "selector": {
          "kind": "operationClassification",
          "value": "mutation"
        },
        "reason": "Validation-only step cannot mutate product files."
      }
    ]
  }
}
```

## Validation Rules

- Reject duplicate keys before materialization.
- Reject missing `SKILL.md`, missing skill `name`, missing `description`, and oversized descriptions that crowd activation context.
- Reject tool templates without input schema or operation classification.
- Reject MCP templates without `allowedTools` for local stdio unless the setup test explicitly records discovered tools and an allowlist decision.
- Reject access policies with invalid enum text, raw string selectors that cannot parse to typed values, ambiguous MCP tool selectors, unsupported selector/effect combinations, or policies that try to grant unassigned capabilities.
- Reject raw secrets, raw headers, and raw environment variables.
- Emit structured errors with template path, key, field name, category, masked detail, correlation ID for setup tests, and repair hint.

## Setup Test Declarations

| Capability type | Required setup declaration | Failure categories |
| --- | --- | --- |
| `externalProcess` tool | fake-safe input payload, timeout, working directory policy, expected output schema, allowed exit codes if not zero-only | `CommandPolicy`, `ProcessStart`, `ProcessExit`, `Timeout`, `JsonParse`, `SchemaValidation`, `SecretBinding` |
| `externalHttp` tool | fake-safe input payload, method, URL template, timeout, expected output schema, header bindings | `HttpStatus`, `Timeout`, `JsonParse`, `SchemaValidation`, `SecretBinding` |
| `localStdio` MCP | command descriptor, startup timeout, list-tools expectation, cleanup expectation, allowed tool decision | `CommandPolicy`, `ProcessStart`, `McpHandshake`, `McpListTools`, `Timeout`, `ResourceCleanup` |
| `remoteHttp` MCP | endpoint descriptor, auth binding, list-tools expectation, timeout | `HttpStatus`, `McpHandshake`, `McpListTools`, `Timeout`, `SecretBinding` |
| `internalHosted` MCP | implementation key, lifecycle owner, list-tools expectation | `ImplementationMissing`, `McpHandshake`, `McpListTools`, `ResourceCleanup` |

Setup tests must be deterministic in automated proof. Real user commands, user-specific secrets, or machine-specific MCP servers are not acceptable proof fixtures.
