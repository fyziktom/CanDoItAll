# C# testability plan

| Phase | Isolated subject | Required proof |
|---|---|---|
| A01 | Logical path value/parser and boundary adapters | Windows/backslash legacy input, Unix input, traversal rejection, canonical serialization, case-independent logical equality policy. |
| A02 | Filesystem path policy and durable writer | Case-sensitive/case-insensitive roots, symlink/reparse escapes, invalid names, deterministic enumeration, cancellation, interrupted replacement. |
| A03 | Control-plane/storage migration coordinator | Dry-run, backup, verification, commit marker, restart continuation, rollback, source-host-required diagnostic. |
| A04 | Vault/provider selection and secret migration | Explicit unsupported profile, headless secure bootstrap, legacy decrypt/re-encrypt, restart, rollback, log/artifact redaction. |
| A05 | Capability composition | Supported/unsupported capability matrices without hidden lifecycle side effects. |
| A06 | Headless host | Startup, health, shutdown, restart, writable/read-only roots, optional desktop absence. |
| A07 | CI/publish | Windows, Ubuntu, macOS focused suites and publish/start smoke. |
| B01-B06 | Execution/runtime/tool boundaries | Typed argv/environment, tree termination, ownership receipts, runtime metadata compilation, Manager recovery, MCP/plugin/process capability behavior. |

## Test seams

- Pure logical-path behavior is tested without filesystem access.
- Physical-path behavior receives an explicit root/case policy and uses disposable filesystem fixtures.
- Secret providers are selected through composition; tests inject provider implementations and never log secret values.
- Process launching consumes a typed plan and injectable executor boundary; tests do not fake success through wrapper-only mocks.
- Migrations expose observable stages and recovery state, not private-method tests.

## Partial-class gate

The current source has 171 partial declarations across 73 type names. A changed partial cluster must demonstrate reduced responsibility count, an independently constructible collaborator, and tests that target the extracted behavior. File-count changes alone are not evidence.

## Baseline handling

Pre-existing failures are recorded separately from regressions. The Windows Integration test host currently stalls before discovery; targeted Components rerun passes. This does not excuse a failing focused suite for changed behavior.
