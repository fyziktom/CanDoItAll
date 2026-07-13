# Error State Inventory

This inventory maps the original request's exception/error-state concern to implementation owners and proof. It must be kept current during execution.

| Error state | Current risk | Target handling | Owner | Tests/proof |
| --- | --- | --- | --- | --- |
| Missing executor id | Node settings fail late or inconsistently. | Validation and runtime diagnostic include node id and repair hint to choose an executor. | SB03/SB06 | Validator negative test and invoker negative test. |
| Executor descriptor unavailable | Plugin grant/trust/source failures can look like missing executor. | Descriptor remains visible but not runnable with explicit availability reason. | SB06/SB08/SB09 | Descriptor parity and unavailable descriptor tests. |
| Executor implementation missing | Descriptor and DI registration can diverge after project split. | Failed composition diagnostic includes executor id, descriptor source, registration owner, and no fallback path. | SB06/SB09 | Catalog composition negative test. |
| Invalid executor settings JSON | Exceptions can lose field/path context. | Diagnostic includes executor id, node id, setting name/path, JSON exception summary, and repair hint. | SB06/SB07/SB08 | Per-category invalid settings tests. |
| Unsafe retry policy | Side-effecting executors can be retried accidentally. | Strong typed side-effect policy blocks unsafe retry and explains idempotency requirement. | SB03/SB06/SB09 | Unsafe retry negative tests. |
| Approval gate missing | Production side effects can fail with generic service error. | Diagnostic includes executor id, node id, permission policy, and missing gate registration. | SB06/SB08/SB09 | Approval gate missing test. |
| Approval denied or expired | User action failure can appear as executor failure. | Diagnostic classifies as approval state, not internal executor error. | SB06/SB07/SB12 | Approval denied and UI display tests. |
| Timeout | Timeout is currently wrapped; context can be lost downstream. | Diagnostic includes timeout seconds, attempt count, executor id, node id, retryability, and repair hint. | SB06/SB07/SB09 | Timeout test per retry policy. |
| Cancellation | Cancellation can be mistaken for failure. | Diagnostic/run state distinguishes user/system cancellation from executor failure. | SB04/SB07/SB11 | Runtime and executor cancellation tests. |
| Payload too large | Output cap errors must remain actionable. | Diagnostic includes payload characters, max allowed, node id, executor id, and artifact/cap hint. | SB06/SB07/SB09 | Payload cap tests. |
| Workspace path denied/escaped | File executors and artifact stores can leak path detail or fail generically. | Diagnostic includes safe path scope, operation, and repair hint without exposing forbidden absolute paths unnecessarily. | SB04/SB07/SB09 | Path traversal and unauthorized path tests. |
| Artifact/checkpoint/store failure | Runtime can partially persist run/event/artifact state. | Persistence policy records what succeeded, what failed, correlation id, and whether retry is safe. | SB04/SB05 | Store/artifact/checkpoint failure tests. |
| Template load/materialization failure | UI-owned loader can hide file/key/node context. | Diagnostic includes template file, template key, workflow key, YAML path, node id, executor id, and repair hint. | SB10/SB13 | All-template and malformed-template tests. |
| MAF compile failure | Compile errors are currently stored as error text/artifact. | Typed payload wraps compile detail with workflow id/version and compiler stage. | SB11/SB13 | Compile failure payload tests. |
| MAF backend failure | Backend or native event failures can collapse to status text. | Diagnostic includes backend kind, MAF event type, node binding, and correlation id. | SB11/SB13 | Backend failure and event normalization tests. |
| LLM/tool/MCP failure | External tools/MCP/plugin calls need exact repair information. | Diagnostic includes provider/server/tool/operation, status/exit code if safe, retryability, redacted provider detail, and artifact reference. | SB11/SB12/SB13 | External tool/MCP negative tests and UI proof. |
| Plugin package load failure | Package assembly/dependency errors can be hard to repair. | Diagnostic includes package id, plugin id, assembly path label, type name if known, dependency name, and restart/install hint. | SB08/SB09 | Package load and missing dependency tests. |
| Plugin activation failure | DI constructor failures can appear as generic activator errors. | Diagnostic includes plugin id, package id, executor type, missing service/dependency when safe, and repair hint. | SB08/SB09 | DI activation negative test. |
| Plugin grant missing | Missing grant must not be treated as missing executor. | Descriptor unavailable diagnostic includes grant id, plugin id, executor id, and grant repair hint. | SB08/SB09/SB12 | Grant negative and UI display tests. |
| OAuth/secret missing or expired | Gmail/Office365 failures risk leaking credentials or vague messages. | Diagnostic identifies connection/secret state with masked identifiers and repair hint to refresh/reconnect. | SB08/SB09/SB12 | OAuth/secret masking tests. |
| Docker host-tool failure | Host command failures need command/exit/output context without leaks. | Diagnostic includes recipe id, plugin id, host tool operation, exit code, capped/redacted output artifact, and approval/grant context. | SB08/SB09 | Docker host command failure tests. |
| Gmail/Office365 external service failure | API errors need Graph/Gmail operation context and retryability. | Diagnostic includes provider operation, status/rate limit when available, retryability, side-effect receipt state, and masked identifiers. | SB08/SB09 | Provider failure and receipt tests. |
| Unknown/unexpected exception | Catch-all paths are still needed but must be useful. | Diagnostic includes source kind, known ids, correlation id, sanitized exception type/message, and secure-log pointer. | SB06-SB14 | No-generic-error audit and redaction tests. |

## Audit Questions For Every Subbundle

- Can a user or agent identify the failing workflow node without reading stack traces?
- Can a user or agent identify whether the failure is validation, runtime, default executor, plugin, external tool/MCP, persistence, artifact, approval, timeout, or cancellation?
- Does the message include a concrete repair hint?
- Are secrets, tokens, full file contents, prompts, command sensitive arguments, and provider raw payloads masked or moved to secure/capped artifacts?
- Does the failure preserve enough source context after crossing API, UI, event sink, audit sink, plugin adapter, and MAF adapter boundaries?
- Are retry decisions explicit and tested?
