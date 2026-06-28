# Error And Diagnostics Model

## Principle

Capability load, setup, and call errors must be structured data first and user-facing text second. Do not throw or return a generic message such as `Error on MCP start` when the failing subsystem can identify the server key, executable, transport, schema field, stderr line, timeout, or rejected secret binding.

## Required Result Shape

Every validator, loader, setup tester, invoker, lifecycle manager, and MAF adapter must return or throw through a typed result/error model with these fields:

| Field | Requirement |
| --- | --- |
| `category` | Strong enum such as `TemplateValidation`, `SecretBinding`, `CommandPolicy`, `ProcessStart`, `ProcessExit`, `Timeout`, `McpHandshake`, `McpListTools`, `SchemaValidation`, `JsonParse`, `HttpStatus`, `Cancellation`, `ImplementationMissing`, `RuntimeAdapter`, `ResourceCleanup`. |
| `capabilityKey` | Required when a catalog capability is involved. |
| `capabilityKind` | Strong enum: `Skill`, `Tool`, `McpServer`, or future known kind. |
| `templatePath` | Required for template/schema/seed failures. |
| `fieldPath` | Required for validation failures when a field is known. |
| `implementationKey` | Required when an internal tool, registered skill, or internal MCP implementation cannot bind. |
| `transport` | Required for external tool/MCP setup failures, for example `ExternalProcess`, `ExternalHttp`, `LocalStdio`, `RemoteHttp`, or `InternalHosted`. |
| `exitCode` | Required for process exits after start. |
| `httpStatusCode` | Required for HTTP setup/call failures. |
| `timeout` | Required for timeout failures. |
| `correlationId` | Required for every setup test and runtime call. |
| `maskedDetail` | Bounded diagnostic detail with secrets masked and size-limited. |
| `repairHint` | Short actionable guidance: missing executable, invalid JSON, rejected command, empty `allowedTools`, schema mismatch, startup timeout, etc. |

## External Tool Failure States

External process and HTTP tool setup/calls must have tests for:

| Failure | Required diagnostic |
| --- | --- |
| Executable/script not found | executable path, configured working directory, capability key, repair hint. |
| Command rejected by policy | rejected command/token, policy name, capability key, repair hint. |
| Working directory invalid or outside allowed root | configured path, resolved path if safe, policy name. |
| Secret binding missing | binding key, destination name, no raw secret value. |
| Process fails to start | exception type, sanitized message, executable path, capability key. |
| Timeout | configured timeout, elapsed time, partial bounded stdout/stderr if any. |
| Non-zero exit | exit code, bounded stdout/stderr, JSON parse status, repair hint. |
| Invalid JSON output | parser error location when available, bounded output excerpt. |
| Output schema mismatch | schema path, field path, expected type, actual type. |
| HTTP transport failure | URL template host, method, status code when available, bounded response, no raw auth header. |
| Cancellation | caller/request correlation ID and whether cleanup completed. |

## MCP Failure States

Internal hosted, local stdio, and remote HTTP MCP setup/runtime must have tests for:

| Failure | Required diagnostic |
| --- | --- |
| Descriptor missing transport/lifecycle owner | template path, field path, capability key. |
| Local command rejected | command, policy name, capability key, no secret values. |
| Server process exits before handshake | exit code, bounded stdout/stderr, elapsed startup time. |
| Startup timeout | timeout, elapsed time, transport, cleanup result. |
| Handshake fails | MCP phase, sanitized exception, transport, server key. |
| `tools/list` fails | phase `ListTools`, server key, bounded protocol error/response. |
| `allowedTools` mismatch | requested tool name, discovered tool names, missing/extra names. |
| Empty discovered tools | server key, transport, setup command, repair hint. |
| Remote HTTP non-success | method, endpoint host/path, status code, bounded response. |
| Resource cleanup failure | process ID when safe, cleanup action, exception type, follow-up action. |

## Logging Rules

- Log category, capability key, capability kind, template path, implementation key, transport, and correlation ID.
- Mask secret values, bearer tokens, API keys, auth headers, raw environment values, and user-supplied command arguments marked secret.
- Bound stdout, stderr, protocol payload, and HTTP response excerpts before storing or showing them.
- Prefer one structured event per failure phase over a stack of low-signal logs.
- Do not catch and continue with missing tools, missing skills, failed templates, or failed MCP setup unless the descriptor explicitly marks the capability optional and tests cover that behavior.

## UI/API Rules

- API responses must expose the typed category and repair hint, not just a localized message.
- UI must show the category-specific repair detail and keep raw diagnostic excerpts expandable, bounded, and masked.
- Save cannot convert a failed setup test into success. If save is allowed after a failed test, the persisted capability must show an explicit warning state and tests must cover it.
