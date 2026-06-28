# 08 MAF Reconnection And Compatibility

## Status

- `Ready after SB07`

## Objective

- Reconnect MAF runtime composition to the isolated Skill, Tool, MCP, template-backed capability services, and effective capability access set while preserving execution behavior.

## Success Criteria

- MAF no longer owns private Skill/MCP/Tool configuration DTOs or hardcoded capability switches for active runtime paths.
- Runtime composition attaches equivalent skills, tools, and MCP tools through adapter services.
- Runtime composition attaches only the `EffectiveCapabilitySet`; MAF does not apply a second private suppression pass.
- Existing execution capability filtering and process policy behavior remain unchanged.
- Runtime failures preserve structured diagnostics from the underlying loader/invoker/lifecycle services.
- Denied required capabilities and suppressed attachments are recorded as structured diagnostics with rule scope, selector, reason, and repair hint.

## Covered Inputs

- R01, R02, R08, R09, R11, R12, R13, R14, R15.

## Prerequisites

- SB02 proof passes.
- SB03 proof passes.
- SB04 proof passes.
- SB05 hardening proof passes.
- SB06 seed parity proof passes.
- SB07 seed/template hardening proof passes.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Skills.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `repo://tests/CanDoItAll.Tests.Integration/AgentFrameworkExecutionCapabilityFilteringIntegrationTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs`
- `bundle://architecture/03-error-and-diagnostics-model.md`
- `bundle://architecture/04-implementation-quality-guardrails.md`
- `bundle://architecture/05-capability-access-policy.md`
- `bundle://inventories/04-capability-access-policy-test-inventory.md`
- `bundle://analysis/03-codeanalytics-and-performance-review.md`

## Deliverables

- MAF adapter services for skills, tools, and MCPs.
- Runtime access policy adapter that builds the evaluation context from agent, process, workflow, and runtime metadata once and passes the effective set to attachers.
- Removed or compatibility-wrapped hardcoded MAF builders.
- Integration tests proving runtime composition from template-backed capability catalog.
- Integration tests proving process/workflow suppression of a skill, tool, MCP server, and MCP tool through the shared evaluator.
- Explicit logs for capability attachment failures with actionable capability key and kind.
- Adapter diagnostics preserving underlying category, transport, template path, implementation key, and repair hint where applicable.

## Dependency Impact

- SB09 hardens runtime reconnection before UI/API work.
- SB10 depends on runtime-compatible setup services.
- SB11 depends on MAF parity for process/workflow regression.
- SB12 cleanup depends on knowing which old code is dead.

## Validation Depth

- `Critical runtime reconnection`

## Implementation Steps

1. Introduce adapter interfaces that convert isolated descriptors into MAF runtime objects.
2. Build an access evaluation context from agent assignments, process/workflow metadata, compatibility `AllowedOperations`, and runtime flags.
3. Evaluate candidates once and pass the `EffectiveCapabilitySet` into skill/tool/MCP attachers.
4. Replace MAF tool switch attachment with tool service/adapters.
5. Replace MAF skill builder with skill service/adapters.
6. Replace MAF MCP builder with MCP runtime/adapters.
7. Replace hardcoded process-step skill exclusion and workspace-tool gating with policy rules or compatibility adapters that feed the shared evaluator.
8. Preserve existing progress logging and failure messages where useful, but include capability key/kind and suppression rule details.
9. Add integration tests for representative existing capability sets.
10. Prove old hardcoded branches and filters are not active runtime fallback paths.
11. Keep adapters split by capability kind and avoid adding more capability orchestration to already-large MAF files.
12. Add tests for missing implementation key, denied required capability, failed external tool setup, failed MCP list-tools, and template validation failure flowing through MAF diagnostics.

## Scope Exceptions

- Do not remove old code until tests prove it is no longer active or until SB12 cleanup.
- Do not add new user-facing UI in this subbundle.

## Do Not Do

- Do not reconnect one capability kind using old logic while claiming full migration.
- Do not catch template or adapter failures and continue with missing tools.
- Do not collapse structured diagnostics into generic runtime exceptions.
- Do not keep `AllowedOperations` string comparisons in MAF runtime attach paths after typed compatibility conversion exists.
- Do not let MAF hide a capability without producing a suppression diagnostic.

## Acceptance Checklist

- Runtime can attach existing workspace, .NET, skill, and MCP capabilities.
- Capability filtering diagnostics remain stable.
- Process/workflow capability restrictions can deny a skill, tool, MCP server, and MCP tool.
- Missing implementation key fails predictably.
- Tool metadata still flows into receipts/ownership where applicable.
- Runtime failure diagnostics include capability key, kind, category, and repair hint.
- Denied required capability diagnostics identify the required source and denying policy rule.
- MAF active capability paths no longer own private Skill/Tool/MCP config DTOs.

## Proof Required

- MAF adapter unit tests.
- Runtime composition integration tests.
- Existing capability filtering test suite.
- Effective capability set and suppression diagnostics integration tests.
- Diagnostics propagation tests.
- Static scan proving old hardcoded paths are not active fallback.
- `proof/SB08/manifest.md`
- `proof/SB08/semantic-invariants.md`

## Browser Validation Logging

- N/A unless runtime proof uses UI manually. Browser regression is SB11.

## Progression Gate

- SB09 cannot proceed until SB08 proves active MAF runtime paths use the new services and preserve compatibility.

## Suggested Agent Prompt

```text
Implement subbundle SB08 only. Reconnect MAF to the isolated capability services and effective capability access set after confirming SB02-SB07 proof. Preserve runtime behavior and fail explicitly with structured diagnostics on denied required capabilities, missing templates, or missing implementations. Do not add UI yet.
```

