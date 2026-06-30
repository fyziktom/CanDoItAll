# SB04 Semantic Invariants

## SB04_INV_INTERNAL_001

- Source raw note: internal hosted MCP servers must have typed descriptors before MAF reconnection.
- Expected behavior: an internal hosted MCP descriptor uses application lifecycle ownership, preserves its typed implementation key, exposes a shared capability descriptor, and can run deterministic setup with an allowed tool.
- Disallowed shallow implementation: treat internal hosted MCP as local process config or drop the implementation key before policy evaluation.
- Failing-first proof: `bundle://proof/SB04/transcripts/failing-first-mcp-runtime-contracts.txt`
- Passing proof: `bundle://proof/SB04/transcripts/passing-mcp-runtime-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB04/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Mcp.Abstractions/Mcp.cs`, `repo://src/CanDoItAll.AgentFramework.Mcp/Descriptors/McpDescriptorFactory.cs`, `repo://src/CanDoItAll.AgentFramework.Mcp/Descriptors/McpExposureDescriptorFactory.cs`, `bundle://proof/SB04/transcripts/source-assertions.txt`
- Red-team negative case: `SB04_INV_POLICY_001` proves implementation metadata flows into the shared policy descriptor without a separate MCP filter path.
- Downstream dependency check: SB08 can adapt internal hosted MCP registrations without keeping internal MCP details inside MAF runtime code.

## SB04_INV_LOCAL_001

- Source raw note: local stdio MCP setup must start, list tools, filter allowed tools, and clean up deterministically.
- Expected behavior: a local stdio descriptor starts through the runtime client, lists discovered tools, returns only explicitly allowed tools, and completes cleanup.
- Disallowed shallow implementation: mark local MCP setup successful without list-tools proof or cleanup evidence.
- Failing-first proof: `bundle://proof/SB04/transcripts/failing-first-mcp-runtime-contracts.txt`
- Passing proof: `bundle://proof/SB04/transcripts/passing-mcp-runtime-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB04/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Mcp/Runtime/McpSetupTestService.cs`, `repo://src/CanDoItAll.AgentFramework.Mcp/Fakes/FakeMcpServerScript.cs`, `bundle://proof/SB04/transcripts/source-assertions.txt`
- Red-team negative case: `SB04_INV_SETUP_002` proves configured but undiscovered tools fail instead of being accepted.
- Downstream dependency check: SB10 can invoke setup/list-tools flows without MAF-specific browser MCP launch logic.

## SB04_INV_LOCAL_002

- Source raw note: local MCP descriptors must not launch without explicit allowed tools.
- Expected behavior: a local stdio descriptor with empty `allowedTools` fails before client creation with `TemplateValidation` at `$.allowedTools`.
- Disallowed shallow implementation: start a local process to discover tools without making the setup decision explicit.
- Failing-first proof: `bundle://proof/SB04/transcripts/failing-first-mcp-runtime-contracts.txt`
- Passing proof: `bundle://proof/SB04/transcripts/passing-mcp-runtime-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB04/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Mcp/Runtime/McpSetupTestService.cs`, `bundle://proof/SB04/transcripts/source-assertions.txt`
- Red-team negative case: `SB04_INV_LOCAL_002` asserts `CreatedClients == 0`.
- Downstream dependency check: SB06/SB10 can block incomplete templates before any local stdio process is launched.

## SB04_INV_LOCAL_003

- Source raw note: local MCP process commands must keep the existing command policy restriction.
- Expected behavior: a disallowed local MCP command fails before client creation with `CommandPolicy` and an actionable repair hint.
- Disallowed shallow implementation: accept arbitrary command strings from templates or UI setup.
- Failing-first proof: `bundle://proof/SB04/transcripts/failing-first-mcp-runtime-contracts.txt`
- Passing proof: `bundle://proof/SB04/transcripts/passing-mcp-runtime-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB04/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Mcp/Runtime/McpSetupTestService.cs`, `repo://src/CanDoItAll.AgentFramework.Core/Mcp/LocalMcpCommandPolicy.cs`, `bundle://proof/SB04/transcripts/source-assertions.txt`
- Red-team negative case: `SB04_INV_LOCAL_003` uses `cmd.exe` and asserts no client is created.
- Downstream dependency check: SB10 setup UI/API can reuse the same command-policy result instead of duplicating local process restrictions.

## SB04_INV_SECRET_001

- Source raw note: raw environment variables and raw headers must remain rejected.
- Expected behavior: local raw environment variables and remote raw headers fail with `SecretBinding` diagnostics and masked detail.
- Disallowed shallow implementation: persist raw secrets, report raw secret values, or silently convert raw values into bindings.
- Failing-first proof: `bundle://proof/SB04/transcripts/failing-first-mcp-runtime-contracts.txt`
- Passing proof: `bundle://proof/SB04/transcripts/passing-mcp-runtime-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB04/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Mcp/Runtime/McpSetupTestService.cs`, `repo://src/CanDoItAll.AgentFramework.Mcp/Diagnostics/McpDiagnostics.cs`, `bundle://proof/SB04/transcripts/source-assertions.txt`
- Red-team negative case: `SB04_INV_SECRET_001` injects `raw-secret-value` into both local and remote descriptors and asserts it is absent from masked diagnostics.
- Downstream dependency check: SB06/SB10 can store binding references only and reject raw secret payloads through one runtime validator.

## SB04_INV_SETUP_001

- Source raw note: `tools/list` failures must be classified and cleanup must still run.
- Expected behavior: a list-tools failure returns `McpListTools`, masks sensitive detail, and records successful cleanup.
- Disallowed shallow implementation: collapse list-tools failure into a generic startup error or skip cleanup after list failure.
- Failing-first proof: `bundle://proof/SB04/transcripts/failing-first-mcp-runtime-contracts.txt`
- Passing proof: `bundle://proof/SB04/transcripts/passing-mcp-runtime-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB04/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Mcp/Runtime/McpSetupTestService.cs`, `repo://src/CanDoItAll.AgentFramework.Mcp/Diagnostics/McpDiagnostics.cs`, `bundle://proof/SB04/transcripts/source-assertions.txt`
- Red-team negative case: the fake list-tools exception includes a token-like secret and expects masked output.
- Downstream dependency check: SB10 setup can show list-tools failure and cleanup state without guessing which phase failed.

## SB04_INV_CLEANUP_001

- Source raw note: cleanup failures must be classified and must not hide the original setup failure.
- Expected behavior: when list-tools fails and stop also fails, the result keeps the original `McpListTools` diagnostic and appends `ResourceCleanup` with masked detail.
- Disallowed shallow implementation: return only `CleanupCompleted=false` or replace the original setup diagnostic with cleanup information.
- Failing-first proof: `bundle://proof/SB04/transcripts/failing-first-mcp-runtime-contracts.txt`
- Passing proof: `bundle://proof/SB04/transcripts/passing-mcp-runtime-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB04/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Mcp/Runtime/McpSetupTestService.cs`, `repo://src/CanDoItAll.AgentFramework.Mcp/Diagnostics/McpDiagnostics.cs`, `bundle://proof/SB04/transcripts/source-assertions.txt`
- Red-team negative case: cleanup exception text includes `raw-secret-value` and the cleanup diagnostic masks it.
- Downstream dependency check: SB05 can harden diagnostic handling knowing cleanup is a first-class phase.

## SB04_INV_SETUP_002

- Source raw note: allowed MCP tools must be server-scoped and validated against discovered tools.
- Expected behavior: if configured allowed tools are missing from `tools/list`, setup fails with `McpListTools` at `$.allowedTools` and names the missing tool.
- Disallowed shallow implementation: trust configured `allowedTools` without comparing them to discovery output.
- Failing-first proof: `bundle://proof/SB04/transcripts/failing-first-mcp-runtime-contracts.txt`
- Passing proof: `bundle://proof/SB04/transcripts/passing-mcp-runtime-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB04/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Mcp/Runtime/McpSetupTestService.cs`, `bundle://proof/SB04/transcripts/source-assertions.txt`
- Red-team negative case: `SB04_INV_SETUP_002` allows `browser_missing` while the fake server returns only `browser_snapshot`.
- Downstream dependency check: SB06 template loading and SB10 setup can enforce `allowedTools` parity before enabling a server.

## SB04_INV_SETUP_003

- Source raw note: setup timeout must be structured and actionable.
- Expected behavior: startup timeout returns `Timeout`, includes descriptor timeout, masks sensitive detail, and attempts cleanup.
- Disallowed shallow implementation: let timeout throw through or report it as a generic runtime adapter error.
- Failing-first proof: `bundle://proof/SB04/transcripts/failing-first-mcp-runtime-contracts.txt`
- Passing proof: `bundle://proof/SB04/transcripts/passing-mcp-runtime-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB04/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Mcp/Runtime/McpSetupTestService.cs`, `repo://src/CanDoItAll.AgentFramework.Mcp/Diagnostics/McpDiagnostics.cs`, `bundle://proof/SB04/transcripts/source-assertions.txt`
- Red-team negative case: timeout exception text includes `raw-secret-value` and the diagnostic masks it.
- Downstream dependency check: SB10 can display timeout-specific repair guidance instead of a generic failure.

## SB04_INV_SETUP_004

- Source raw note: startup and handshake failures must remain separate diagnostic phases.
- Expected behavior: process start failures return `ProcessStart`; protocol initialization failures return `McpHandshake`; both mask sensitive detail and complete cleanup.
- Disallowed shallow implementation: report all setup failures as `McpListTools` or a generic startup error.
- Failing-first proof: `bundle://proof/SB04/transcripts/failing-first-mcp-runtime-contracts.txt`
- Passing proof: `bundle://proof/SB04/transcripts/passing-mcp-runtime-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB04/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Mcp/Runtime/McpSetupTestService.cs`, `repo://src/CanDoItAll.AgentFramework.Mcp/Runtime/McpSetupException.cs`, `bundle://proof/SB04/transcripts/source-assertions.txt`
- Red-team negative case: both injected exceptions include token-like secret text and expected masked diagnostics.
- Downstream dependency check: SB05/SB10 can branch setup repair guidance by process-start versus handshake phase.

## SB04_INV_SETUP_005

- Source raw note: cancellation must be explicit and cleanup must be attempted.
- Expected behavior: operation cancellation returns `Cancellation`, records cleanup completion, and does not surface an unhandled exception.
- Disallowed shallow implementation: swallow cancellation as success or let cancellation skip cleanup for a started client.
- Failing-first proof: `bundle://proof/SB04/transcripts/failing-first-mcp-runtime-contracts.txt`
- Passing proof: `bundle://proof/SB04/transcripts/passing-mcp-runtime-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB04/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Mcp/Runtime/McpSetupTestService.cs`, `bundle://proof/SB04/transcripts/source-assertions.txt`
- Red-team negative case: the fake client throws `OperationCanceledException` during start and the result keeps cleanup proof.
- Downstream dependency check: setup callers can distinguish user/system cancellation from failed MCP configuration.

## SB04_INV_RUNTIME_001

- Source raw note: fake MCP runtime must support deterministic start, list, call, and stop tests.
- Expected behavior: fake runtime client starts, lists configured tools, returns configured call results, and records lifecycle counters.
- Disallowed shallow implementation: fake only list-tools setup but leave runtime calls untestable.
- Failing-first proof: `bundle://proof/SB04/transcripts/failing-first-mcp-runtime-contracts.txt`
- Passing proof: `bundle://proof/SB04/transcripts/passing-mcp-runtime-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB04/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Mcp/Fakes/FakeMcpServerScript.cs`, `bundle://proof/SB04/transcripts/source-assertions.txt`
- Red-team negative case: `FakeMcpRuntimeClient` returns `ImplementationMissing` when a test calls an unconfigured fake tool.
- Downstream dependency check: SB08/SB11 can add runtime parity tests without real MCP process or network dependencies.

## SB04_INV_POLICY_001

- Source raw note: MCP servers and child tools must use the shared typed access policy/effective-set model.
- Expected behavior: an MCP server descriptor and a server-scoped child MCP tool descriptor are denied by `McpServerKey` and `McpToolName` selectors through the SB01 evaluator.
- Disallowed shallow implementation: keep MCP suppression rules hidden inside MAF or compare opaque tool strings without server context.
- Failing-first proof: `bundle://proof/SB04/transcripts/failing-first-mcp-runtime-contracts.txt`
- Passing proof: `bundle://proof/SB04/transcripts/passing-mcp-runtime-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB04/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Mcp/Descriptors/McpExposureDescriptorFactory.cs`, `bundle://proof/SB04/transcripts/source-assertions.txt`
- Red-team negative case: `SB04_INV_POLICY_001` denies both the server and `browser_snapshot` child tool and expects no allowed capabilities.
- Downstream dependency check: SB08/SB11 can apply the same typed evaluator across MAF, processes, workflows, and UI preview.

## SB04_INV_REMOTE_001

- Source raw note: remote HTTP MCP failures must include typed status diagnostics with masked response detail.
- Expected behavior: a remote HTTP setup failure returns `HttpStatus`, preserves the status code, and masks bearer/authorization detail.
- Disallowed shallow implementation: report remote failures as local process errors or leak raw authorization values.
- Failing-first proof: `bundle://proof/SB04/transcripts/failing-first-mcp-runtime-contracts.txt`
- Passing proof: `bundle://proof/SB04/transcripts/passing-mcp-runtime-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB04/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Mcp/Diagnostics/McpDiagnostics.cs`, `repo://src/CanDoItAll.AgentFramework.Mcp/Runtime/McpSetupTestService.cs`, `bundle://proof/SB04/transcripts/source-assertions.txt`
- Red-team negative case: injected HTTP status detail contains `Authorization=Bearer raw-secret-value` and the diagnostic masks it.
- Downstream dependency check: SB10 remote MCP setup can show status-specific repair guidance without exposing credentials.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `McpServerDescriptor` | `repo://src/CanDoItAll.AgentFramework.Mcp.Abstractions/Mcp.cs`, `repo://src/CanDoItAll.AgentFramework.Mcp/Descriptors/McpDescriptorFactory.cs` | `repo://tests/CanDoItAll.Tests.Unit/McpRuntimeContractsTests.cs` | `SB04_INV_INTERNAL_001`, `SB04_INV_LOCAL_001`, `SB04_INV_REMOTE_001` | `SB04_INV_LOCAL_002`, `SB04_INV_LOCAL_003`, `SB04_INV_SECRET_001` |
| `McpSetupTestResult` | `repo://src/CanDoItAll.AgentFramework.Mcp.Abstractions/Mcp.cs`, `repo://src/CanDoItAll.AgentFramework.Mcp/Runtime/McpSetupTestService.cs` | `repo://tests/CanDoItAll.Tests.Unit/McpRuntimeContractsTests.cs` | `bundle://proof/SB04/transcripts/passing-mcp-runtime-contracts.txt` | `SB04_INV_SETUP_001`, `SB04_INV_CLEANUP_001`, `SB04_INV_SETUP_002`, `SB04_INV_SETUP_003`, `SB04_INV_SETUP_004`, `SB04_INV_SETUP_005` |
| `CapabilityExposureDescriptor` | `repo://src/CanDoItAll.AgentFramework.Mcp/Descriptors/McpExposureDescriptorFactory.cs` | `repo://tests/CanDoItAll.Tests.Unit/McpRuntimeContractsTests.cs` | `SB04_INV_POLICY_001` | `SB04_INV_POLICY_001` |
| `CapabilityDiagnostic` | `repo://src/CanDoItAll.AgentFramework.Mcp/Diagnostics/McpDiagnostics.cs`, `repo://src/CanDoItAll.AgentFramework.Mcp/Runtime/McpSetupTestService.cs` | `repo://tests/CanDoItAll.Tests.Unit/McpRuntimeContractsTests.cs` | `bundle://proof/SB04/transcripts/passing-mcp-runtime-contracts.txt`, `bundle://proof/SB04/transcripts/static-performance-scan.txt` | `SB04_INV_SECRET_001`, `SB04_INV_SETUP_001`, `SB04_INV_CLEANUP_001`, `SB04_INV_SETUP_003`, `SB04_INV_SETUP_004`, `SB04_INV_REMOTE_001` |
| `FakeMcpRuntimeClient` | `repo://src/CanDoItAll.AgentFramework.Mcp/Fakes/FakeMcpServerScript.cs` | `repo://tests/CanDoItAll.Tests.Unit/McpRuntimeContractsTests.cs` | `SB04_INV_LOCAL_001`, `SB04_INV_RUNTIME_001` | `SB04_INV_CLEANUP_001`, `SB04_INV_SETUP_003`, `SB04_INV_SETUP_004`, `SB04_INV_SETUP_005` |

## Anti-Stub Audit

- `bundle://proof/SB04/transcripts/anti-stub-audit.txt`
