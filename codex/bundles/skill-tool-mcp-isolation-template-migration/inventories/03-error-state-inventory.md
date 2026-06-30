# Error State Inventory

## Template And Loader Errors

| Area | Error states to test | Required proof |
| --- | --- | --- |
| Capability pack manifest | missing manifest, invalid schema version, missing file list, duplicate file entry | Unit test reports template path and field path. |
| Common descriptor | duplicate key, invalid key, missing kind, missing stable ID, unknown kind, unknown implementation key | Unit test reports key/kind/path and blocks materialization. |
| Skill loader | missing `SKILL.md`, missing `name`, missing `description`, unsupported script policy, external root not allowed, missing registered descriptor | Unit/integration tests with actionable errors and no fallback instructions. |
| Tool descriptor | missing runtime name, invalid schema, missing operation/side-effect classification, unsupported transport, unsafe command, missing timeout | Unit tests report field path and policy/transport category. |
| MCP descriptor | missing transport, missing lifecycle owner, empty `allowedTools` without setup decision, raw env/header fields, unknown internal server | Unit tests report field path and setup repair hint. |
| Seed materializer | missing default capability, duplicate stable ID, managed seed version churn, agent assignment missing capability | Integration parity test blocks SB08. |

## External Tool Errors

| Area | Error states to test | Required proof |
| --- | --- | --- |
| Process launch | executable not found, access denied, invalid working directory, command rejected by policy | Fake process invoker tests structured `ProcessStart` or `CommandPolicy` category. |
| Process execution | timeout, non-zero exit, stderr present, huge stdout/stderr, cancellation | Tests prove output is bounded, masked, and cleanup completes. |
| Process result | invalid JSON, schema mismatch, missing required field, unexpected content type | Tests prove `JsonParse` or `SchemaValidation` details include field/path. |
| HTTP call | invalid URL template, missing secret header binding, DNS/connect failure, non-success status, timeout, invalid JSON | Tests prove status/bounded response and masked headers. |
| Tool policy | operation not allowed in process context, approval required but missing, receipt ownership missing | Policy tests preserve current behavior. |

## MCP Errors

| Area | Error states to test | Required proof |
| --- | --- | --- |
| Internal hosted MCP | server type missing, DI binding missing, start throws, lifecycle cleanup throws | Unit/integration tests report implementation key and cleanup result. |
| Local stdio MCP | command rejected, executable missing, process exits before handshake, startup timeout, stderr-only failure | Fake MCP tests report phase, exit code, bounded output, and cleanup. |
| Remote HTTP MCP | invalid endpoint, auth binding missing, non-success status, protocol mismatch, timeout | Integration tests report endpoint host/path and status without leaking secrets. |
| Tool discovery | `tools/list` fails, empty discovered tools, discovered tool name invalid, `allowedTools` references missing tool | Setup test reports server key, discovered tools, missing/extra names. |
| Runtime call | MCP tool call timeout, malformed response, server disconnect, cancellation | Runtime tests prove diagnostics preserve capability key and MCP tool name. |

## UI/API Error States

| Area | Error states to test | Required proof |
| --- | --- | --- |
| Setup API | validation failure, setup timeout, command policy rejection, MCP list-tools failure, external tool schema mismatch | API integration tests assert category, repair hint, correlation ID, and masked detail. |
| Setup UI | invalid form, failed setup test, partial MCP discovery, external tool dry-run failure, secret binding missing | Component and Playwright tests show actionable messages without raw secrets. |
| Save behavior | save after failed setup, save with warning, save blocked by critical validation | Tests assert expected state explicitly; no hidden success. |
