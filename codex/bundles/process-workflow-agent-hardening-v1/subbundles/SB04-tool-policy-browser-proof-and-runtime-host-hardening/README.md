# SB04 - Tool Policy, Browser Proof, And Runtime Host Hardening

## Status

Implemented and validated. Classification: **Critical foundation**.

## Objective

Make tool availability, browser proof, runtime command execution, host identity, cleanup, and build-lock behavior deterministic and auditable.

## Covered Inputs

Covers browser proof policy mismatch, runtime command semantics, `workspace_dotnet_run` keepAlive/lifetime, stale browser proof, build output locks, port/database drift, repeated tool invocation guard, and process allowed-operations-to-tools mapping.

## Prerequisites

SB01 completed. SB02 should be completed before final SB04 closure if process dispatch operation contracts are changed.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandPlanBuilder.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Files/WorkspaceFileMutationService.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/*`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs`
- `repo://Templates/Processes/processes/dotnet-runtime-command-writeback/definition.json`
- `repo://Templates/Processes/processes/dotnet-ui-screenshot-writeback/definition.json`
- `repo://codex/skills/candoitall-watch-playwright-loop/SKILL.md`

## Deliverables

- Tool policy decision service extracted or hardened.
- Browser proof artifact schema/validator.
- Runtime host identity record.
- Runtime cleanup receipt validator.
- Build-lock prevention/cleanup tests.
- Allowed-operations-to-tool-availability tests.
- Playwright proof validator tests.
- Proof manifest and semantic invariants for SB04.

## Dependency Impact

SB08 real app tests depend on browser/runtime host proof. SB07 displays these states. SB09 red-teams fake proof and host drift.

## Validation Depth

Deep semantic validation. Must reject stale screenshot/proof, wrong host/DB profile, missing cleanup receipt, and tool denied/allowed mismatch.

## Implementation Steps

1. Trace operation contract -> agent metadata -> tool policy -> runtime tool catalog -> tool invocation.
2. Add tests proving browser tools are available only when allowed and required when process asks for browser proof.
3. Define browser proof schema and validator.
4. Bind browser artifacts to current run/step/execution/host/route/viewport.
5. Harden runtime host launch/keepAlive/cleanup receipts.
6. Add build-lock cleanup test using a controlled child process.
7. Make repeated tool invocation guard diagnostics actionable without losing provider usage.
8. Update templates/skills only if SB06 sync will cover them.

## Scope Exceptions

UI display work belongs to SB07 unless needed for proof capture. Broad skill/template wording changes belong to SB06.

## Do Not Do

- Do not accept screenshots without current-run binding.
- Do not accept browser proof from chat-only claims.
- Do not leave long-running hosts without cleanup receipts.
- Do not use wrong timeout units.
- Do not silently disable browser tools when operation contracts require them.

## Acceptance Checklist

- [x] Tool policy mapping tests pass.
- [x] Browser proof validator rejects stale/copy-only proof.
- [x] Runtime host identity includes host URL and DB profile.
- [x] Cleanup receipts prevent build-lock regression.
- [x] Playwright proof path is documented.
- [x] SB04 proof manifest exists.

## Proof Required


Because this is a critical subbundle, the Semantic Adequacy Gate proof must include:

- `proof/SBxx/manifest.md`
- `proof/SBxx/semantic-invariants.md` or `.json`
- changed-file hashes
- command transcript paths
- source assertions
- shallow-pass trap
- adversarial negative proof
- semantic positive proof
- anti-stub audit
- raw-note literal closure
- dependency smoke proof where stated

Production Behavior Artifact Matrix required for browser proof records, runtime host records, cleanup receipts, and tool policy decisions.


## Browser Validation Logging

Required. Record browser-validation analytics for any changed proof route. Include route, host, DB profile, viewport, Playwright actions, screenshot paths, console messages, cleanup receipt path, and result.

## Progression Gate

SB04 passes only when fake browser proof and wrong-host proof are rejected and runtime hosts are cleaned up reliably.

## Suggested Agent Prompt

Implement SB04 only. Harden tool policy, browser proof, and host lifecycle. Preserve generic process behavior and capture real browser/host artifacts.
