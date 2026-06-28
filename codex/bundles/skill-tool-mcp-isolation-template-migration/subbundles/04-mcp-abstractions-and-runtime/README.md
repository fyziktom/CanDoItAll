# 04 MCP Abstractions And Runtime

## Status

- `Completed`

## Objective

- Build the dedicated MCP abstraction and runtime layer for internal hosted, local stdio, and remote HTTP MCP servers, including lifecycle ownership and setup-time start/list-tools testing.

## Success Criteria

- MCP descriptors model transport, lifecycle, allowed tools, secret bindings, approval mode, and setup tests.
- Fake local/internal MCP servers can be started, listed, called, and stopped deterministically in tests.
- MCP servers and discovered/allowed MCP tools expose common capability exposure descriptors so policies can deny a whole server or a specific child tool.
- Raw environment variables and headers remain rejected.
- MCP failures are classified by startup, handshake, list-tools, allowlist, runtime call, timeout, cancellation, and cleanup phases.

## Covered Inputs

- R01, R02, R05, R08, R09, R11, R12, R13, R14, R15.
- User requirement that MCP setup can test server start and list tools.

## Prerequisites

- SB01 contracts and naming validation pass.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Mcp/LocalMcpCommandPolicy.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Capabilities/CapabilityProofService.Rules.cs`
- `bundle://architecture/03-error-and-diagnostics-model.md`
- `bundle://architecture/04-implementation-quality-guardrails.md`
- `bundle://architecture/05-capability-access-policy.md`
- `bundle://inputs/01-source-artifacts.md`
## Deliverables

- MCP abstraction and implementation projects or folders agreed in SB01.
- Internal hosted MCP lifecycle manager.
- Local stdio MCP client/launcher with explicit ownership and cleanup.
- Remote HTTP MCP client wrapper.
- Setup test service that starts a fake server, lists tools, applies allowed-tool filtering, and reports actionable failures.
- MCP exposure descriptor factory for internal hosted, local stdio, remote HTTP servers, and child MCP tools returned by list-tools.
- Integration with secret binding resolver without raw secret persistence.
- MCP diagnostic mapper for command policy rejection, process start, startup timeout, handshake, `tools/list`, allowedTools mismatch, remote HTTP status, cancellation, and cleanup failures.

## Dependency Impact

- SB05 hardens MCP runtime before template/runtime consumption.
- SB06 uses MCP descriptors for templates and seeding.
- SB08 uses MCP runtime services to replace MAF MCP builder logic.
- SB10 uses setup test/list-tools services.
- SB11 depends on MCP behavior parity for browser and external MCP capability flows.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Map current MCP configuration fields to descriptors.
2. Implement lifecycle ownership model for internal hosted, local stdio, and remote HTTP.
3. Implement deterministic fake MCP server fixture.
4. Implement list-tools setup test with allowed tool enforcement.
5. Implement MCP exposure descriptor factory for server-level and child-tool-level policy matching.
6. Preserve local command policy and secret binding restrictions.
7. Add bounded stdout/stderr/protocol capture for local stdio and masked bounded response capture for remote HTTP.
8. Add unit tests for validation, startup failure, handshake failure, list-tools failure, allowedTools mismatch, timeout, cancellation, lifecycle cleanup, and exposure descriptor policy participation.
9. Add integration tests for fake MCP start/list/call/stop.
10. Record cleanup proof that fake/local server resources do not leak after failed setup.

## Scope Exceptions

- Do not reconnect MAF MCP attachment yet.
- Do not build UI buttons yet.

## Do Not Do

- Do not allow raw environment variables, raw headers, or raw secrets in persisted config.
- Do not let local MCP processes survive test cleanup.
- Do not accept MCP templates without explicit lifecycle ownership.
- Do not report MCP failures as a single generic startup error.
- Do not model MCP tool restrictions as opaque strings without server context.

## Acceptance Checklist

- Fake MCP setup test reports discovered tools.
- Missing `allowedTools` fails for local MCP unless the descriptor explicitly routes through setup decision flow.
- Startup, handshake, and list-tools failures return actionable diagnostics with sensitive data masked.
- Internal hosted MCP lifecycle does not leak processes or service instances.
- Cleanup failures are captured as diagnostics and do not hide the original failure.
- MCP exposure descriptors can be denied by server key and by server-scoped MCP tool name through the shared policy evaluator.

## Proof Required

- MCP runtime and setup-test unit tests.
- Fake MCP integration test with list-tools and cleanup proof.
- Diagnostics samples for startup timeout, list-tools failure, and allowedTools mismatch.
- Access-policy participation tests for server-level and child-tool-level MCP descriptors.
- `proof/SB04/manifest.md`
- `proof/SB04/semantic-invariants.md`

## Execution Proof

- Added `CanDoItAll.AgentFramework.Mcp.Abstractions` and `CanDoItAll.AgentFramework.Mcp`.
- Implemented typed MCP server descriptors for internal hosted, local stdio, and remote HTTP transports.
- Implemented deterministic fake MCP client/runtime fixtures and setup-test service with start, list-tools, allowed-tools filtering, cleanup, cancellation, timeout, process-start, handshake, list-tools, HTTP status, and cleanup diagnostics.
- Preserved local command-policy checks through `LocalMcpCommandPolicy`.
- Rejected raw environment variables and raw headers before client creation.
- Mapped MCP servers and discovered child tools into shared `CapabilityExposureDescriptor` instances for SB01 access-policy evaluation.
- Targeted MCP runtime contract tests passed: `bundle://proof/SB04/transcripts/passing-mcp-runtime-contracts.txt`.
- Full solution build passed with 0 warnings and 0 errors: `bundle://proof/SB04/transcripts/dotnet-build-solution.txt`.
- Source assertions, anti-stub audit, static/performance scan, and file-size scan passed: `bundle://proof/SB04/transcripts/source-assertions.txt`, `bundle://proof/SB04/transcripts/anti-stub-audit.txt`, `bundle://proof/SB04/transcripts/static-performance-scan.txt`, `bundle://proof/SB04/transcripts/file-size-scan.txt`.
- Critical proof manifest and semantic invariants are recorded at `bundle://proof/SB04/manifest.md` and `bundle://proof/SB04/semantic-invariants.md`.

## Browser Validation Logging

- N/A for runtime work. UI proof is SB10.

## Progression Gate

- Passed. SB05 is unblocked because SB04 proves MCP start/list/cleanup behavior, cleanup failure diagnostics, command policy, secret restrictions, and shared policy participation.

## Suggested Agent Prompt

```text
Implement subbundle SB04 only. Build MCP descriptors, runtime lifecycle, exposure descriptors, and setup test services. Use a deterministic fake MCP server. Preserve current secret and command-policy restrictions. Do not reconnect MAF or UI yet.
```

