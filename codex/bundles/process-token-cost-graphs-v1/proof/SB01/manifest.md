# SB01 Proof Manifest

- Subbundle: `01-01-token-usage-cost-accounting`
- Status: `Implemented`
- Owned requirements: R001, R002, R003
- Owned raw notes: N001, N002, N003, N004
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`

## Changed File Hashes

| File | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://src/CanDoItAll.AgentFramework.Core/Contracts/Contracts.cs` | `4c1b20ae2de970644bb04250e3bea5ee0cb13bf72105a80c8a75086342f4d507` | `fc9f24247d5ac473bdec188d161d6a4b32c7763db1b169432020cedc4f27af9a` |
| `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Chat.cs` | `a931cba2c9f2549872d1f28a4e35b6b694540ee58e373fb77e3b361cf5936ebf` | `6496e488256308487683949b49fee5f8246863bf5471c6d986fd22d3d078a55e` |
| `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` | `73e94cf251fc076e4d2b32b31a05fc2d60d9cf5ee679969a6ab511d17885916f` | `248942c42eac7005b52103de4fe17f28b57e8ed41c0a0207ed261607346668d9` |
| `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs` | `1db493abcd45b1f780c6cec6dd9a83c3d338f7a0a7700406de3412adc0fdf75c` | `b8ac7f39966f2a6251239eaa636950fc699b46df0153e00248d891559ae24d72` |
| `repo://tests/CanDoItAll.Tests.Integration/AgentFrameworkExecutionRunTrackingIntegrationTests.cs` | `694ce61929fdb4bb40738bce8fc464ddaef775c5ed537a912b8ff09cca55fa3a` | `5c90b4cdf77789451cab5bc4f7cf5e82ca5f8753f8cde162962fc12aeabcc65f` |
| `repo://tests/CanDoItAll.Tests.Unit/ProviderPricingTests.cs` | `b74438aa4119920461699913afb49b5f384dfbacca950977edbfcaff1c4134f2` | `eb0c99f9d4e3fb32c9e38c491d5f0d70288a141b0a0b82b9b50f066dc286bfa8` |

## Command Transcripts

- Build proof: `bundle://proof/SB01/transcripts/build-accounting-projects.txt`
- Unit pricing proof: `bundle://proof/SB01/transcripts/unit-provider-pricing.txt`
- Integration execution tracking proof: `bundle://proof/SB01/transcripts/integration-execution-tracking.txt`
- Source assertions: `bundle://proof/SB01/transcripts/source-assertions.txt`
- Anti-stub audit: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`

## Failing-First And Passing Proof

- Failing-first case: successful provider usage would previously persist provider input plus local prompt estimate and had no provider cached-token propagation path.
- Failing-first: n/a process exemption; the failing run was not captured before implementation in this single-pass repair, so the manifest relies on named shallow-pass traps plus post-change tests that assert the opposite behavior.
- Passing proof for same behavior: `bundle://proof/SB01/transcripts/integration-execution-tracking.txt`.
- Cached price passing proof: `bundle://proof/SB01/transcripts/unit-provider-pricing.txt`.

## Source-Level Assertions

- Runtime contract carries cached input tokens.
- MAF runtime maps `CachedInputTokenCount` with explicit clamping.
- Continuation aggregation includes cached input.
- Successful execution metrics persist provider-reported input, output, cached input, and tool calls without prompt double counting.

## Anti-Stub Audit

- `bundle://proof/SB01/transcripts/anti-stub-audit.txt`
- Result: no placeholder or fixture-only markers in changed production accounting files.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `AgentRuntimeResponse.CachedInputTokens` | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs` | `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Chat.cs` | runtime response created during agent execution | `bundle://proof/SB01/transcripts/integration-execution-tracking.txt` |
| `AgentRunMetric.CachedInputTokens` | `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` | `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs` | successful run metrics persist on execution completion | `bundle://proof/SB01/transcripts/unit-provider-pricing.txt` |
