# SB04 Proof Manifest

## Status

- Subbundle: `SB04`
- Status: `Completed`
- Validation depth: `Critical foundation`
- Owned requirements: R01, R02, R05, R08, R09, R11, R12, R13, R14, R15
- Owned raw notes: dedicated MCP abstraction and implementation projects; internal hosted, local stdio, and remote HTTP MCP descriptors; deterministic setup tests; list-tools and allowed-tools validation; typed server/tool exposure descriptors; secret and command-policy restrictions; phase-specific diagnostics with cleanup proof.

## Semantic Contract

- `bundle://proof/SB04/semantic-invariants.md`

## Changed Files

- `bundle://proof/SB04/changed-file-hashes.txt`

## Command Transcripts

- Failing-first targeted tests: `bundle://proof/SB04/transcripts/failing-first-mcp-runtime-contracts.txt`
- Passing targeted tests: `bundle://proof/SB04/transcripts/passing-mcp-runtime-contracts.txt`
- Full build: `bundle://proof/SB04/transcripts/dotnet-build-solution.txt`
- Source assertions: `bundle://proof/SB04/transcripts/source-assertions.txt`
- Anti-stub audit: `bundle://proof/SB04/transcripts/anti-stub-audit.txt`
- Static/performance scan: `bundle://proof/SB04/transcripts/static-performance-scan.txt`
- File-size scan: `bundle://proof/SB04/transcripts/file-size-scan.txt`

## Failing-First Proof

- `bundle://proof/SB04/transcripts/failing-first-mcp-runtime-contracts.txt`
- The transcript captures the focused SB04 tests failing to compile before `CanDoItAll.AgentFramework.Mcp` and `.Mcp.Abstractions` existed. That proves the MCP runtime contract tests were introduced before the implementation layer.

## Passing Proof

- `bundle://proof/SB04/transcripts/passing-mcp-runtime-contracts.txt`
- `bundle://proof/SB04/transcripts/dotnet-build-solution.txt`
- The passing transcript includes 14 targeted tests covering internal hosted descriptors, local stdio setup/list/cleanup, missing allowed tools, command policy, secret binding rejection, list-tools failure, cleanup failure diagnostics, allowed-tools mismatch, timeout, process-start and handshake diagnostics, cancellation, fake start/list/call/stop, shared access-policy participation, and remote HTTP status diagnostics.

## Source Assertions

- `repo://src/CanDoItAll.AgentFramework.Mcp.Abstractions/Mcp.cs`
- `repo://src/CanDoItAll.AgentFramework.Mcp/Descriptors/McpDescriptorFactory.cs`
- `repo://src/CanDoItAll.AgentFramework.Mcp/Descriptors/McpExposureDescriptorFactory.cs`
- `repo://src/CanDoItAll.AgentFramework.Mcp/Diagnostics/McpDiagnostics.cs`
- `repo://src/CanDoItAll.AgentFramework.Mcp/Runtime/McpSetupException.cs`
- `repo://src/CanDoItAll.AgentFramework.Mcp/Runtime/McpSetupTestService.cs`
- `repo://src/CanDoItAll.AgentFramework.Mcp/Fakes/FakeMcpServerScript.cs`
- `repo://tests/CanDoItAll.Tests.Unit/McpRuntimeContractsTests.cs`
- Source assertion transcript: `bundle://proof/SB04/transcripts/source-assertions.txt`

## Anti-Stub Audit

- Command transcript: `bundle://proof/SB04/transcripts/anti-stub-audit.txt`
- Result: no production `TODO`, `NotImplemented`, known shallow-stub return patterns, or fake markers under non-fixture SB04 production files. The deterministic fake MCP fixture is intentional test infrastructure and is covered by source assertions and targeted tests.

## Browser Or Host Proof

- Browser proof: N/A. SB04 has no browser-visible surface; SB10/SB11 will carry large-screen-only UI proof per user instruction.
- Host proof: deterministic fake MCP clients prove setup start/list/cleanup and runtime start/list/call/stop behavior without launching real local processes. MAF attachment remains SB08/SB10/SB11 scope.

## Downstream Smoke Proof

- `bundle://proof/SB04/transcripts/dotnet-build-solution.txt` proves the MCP abstraction and implementation projects compile inside `CanDoItAll.slnx`.
- `bundle://proof/SB04/transcripts/passing-mcp-runtime-contracts.txt` proves MCP descriptors consume SB01 typed capability contracts and participate in the shared access-policy evaluator before SB05/SB06 consume the foundation.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `McpServerDescriptor` | `repo://src/CanDoItAll.AgentFramework.Mcp.Abstractions/Mcp.cs` and `repo://src/CanDoItAll.AgentFramework.Mcp/Descriptors/McpDescriptorFactory.cs` define internal hosted, local stdio, and remote HTTP descriptors with lifecycle owner, allowed tools, approval mode, and secret binding fields. | `repo://tests/CanDoItAll.Tests.Unit/McpRuntimeContractsTests.cs` consumes all three descriptor families. | `SB04_INV_INTERNAL_001`, `SB04_INV_LOCAL_001`, and `SB04_INV_REMOTE_001` exercise descriptor-specific setup behavior. | `SB04_INV_LOCAL_002`, `SB04_INV_LOCAL_003`, and `SB04_INV_SECRET_001` reject unsafe local and remote descriptors before runtime start. |
| `McpSetupTestResult` | `repo://src/CanDoItAll.AgentFramework.Mcp.Abstractions/Mcp.cs` defines success/failure shape with discovered tools, allowed tools, diagnostics, correlation ID, and cleanup proof. | `repo://tests/CanDoItAll.Tests.Unit/McpRuntimeContractsTests.cs` asserts success, failure, diagnostics, and cleanup state. | `bundle://proof/SB04/transcripts/passing-mcp-runtime-contracts.txt` runs setup through start, list-tools, allowlist, cleanup, timeout, cancellation, and error phases. | `SB04_INV_SETUP_001`, `SB04_INV_CLEANUP_001`, `SB04_INV_SETUP_002`, `SB04_INV_SETUP_003`, `SB04_INV_SETUP_004`, and `SB04_INV_SETUP_005` prove no generic setup failure hides typed phase state. |
| `CapabilityExposureDescriptor` | `repo://src/CanDoItAll.AgentFramework.Mcp/Descriptors/McpExposureDescriptorFactory.cs` maps MCP servers and discovered child tools into SB01 shared exposure descriptors. | `repo://tests/CanDoItAll.Tests.Unit/McpRuntimeContractsTests.cs` evaluates server and child-tool descriptors through `CapabilityAccessPolicyEvaluator` in `SB04_INV_POLICY_001`. | The exposure factory preserves server key, MCP tool name, tags, operation classifications, side-effect profile, availability, and template path. | `SB04_INV_POLICY_001` denies both a whole MCP server and one server-scoped child MCP tool without MCP-specific suppression logic. |
| `CapabilityDiagnostic` | `repo://src/CanDoItAll.AgentFramework.Mcp/Diagnostics/McpDiagnostics.cs` emits typed diagnostic category, transport, timeout/status details, field path, masked detail, repair hint, and correlation ID. | `repo://tests/CanDoItAll.Tests.Unit/McpRuntimeContractsTests.cs` asserts `SecretBinding`, `CommandPolicy`, `McpListTools`, `ResourceCleanup`, `Timeout`, `ProcessStart`, `McpHandshake`, `Cancellation`, and `HttpStatus`. | `bundle://proof/SB04/transcripts/static-performance-scan.txt` proves diagnostics live in a no-MAF/no-Blazor implementation project with no sync-over-async/reflection matches. | Secret-bearing diagnostics are red-team tested with raw environment variables, raw headers, list-tools failure, timeout, process-start, handshake, cleanup, and HTTP status text. |
| `FakeMcpRuntimeClient` | `repo://src/CanDoItAll.AgentFramework.Mcp/Fakes/FakeMcpServerScript.cs` defines deterministic fake MCP start/list/call/stop behavior and injectable exceptions. | `repo://tests/CanDoItAll.Tests.Unit/McpRuntimeContractsTests.cs` uses the fake factory/client for setup and runtime tests. | `SB04_INV_RUNTIME_001` proves start/list/call/stop counters and configured tool result flow. | `SB04_INV_CLEANUP_001`, `SB04_INV_SETUP_003`, `SB04_INV_SETUP_004`, and `SB04_INV_SETUP_005` inject failures without real process leakage. |
