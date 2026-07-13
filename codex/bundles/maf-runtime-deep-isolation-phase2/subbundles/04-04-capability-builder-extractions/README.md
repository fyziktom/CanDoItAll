# SB04 Capability Builder Extractions

## Status

- `Ready`

## Objective

Extract context, skill, tool, and MCP builders from private nested `MafAgentRuntime` classes into named top-level components with direct tests and no dependency on `MafAgentRuntime owner`.

## Covered Inputs

- N002, N003, N004, N005, N006, N007
- MAF2-R004, MAF2-R005, MAF2-R010

## Prerequisites

- SB01 closure proof.
- SB02 closure proof.
- SB03 closure proof.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Context.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Skills.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.ConfiguredWorkspace.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs`

## Deliverables

- Top-level `ContextCapabilityBuilder` and context providers.
- Top-level `SkillCapabilityBuilder`.
- Top-level `ToolCapabilityBuilder` or smaller tool contributors.
- Top-level `McpCapabilityBuilder` split into smaller collaborators where behavior warrants it.
- Top-level MCP wrappers/leases for local MCP tools and browser MCP compaction.
- Direct tests for each builder and important negative cases.

## Dependency Impact

- SB05 workspace drivers and SB06 execution recovery rely on clean tool/MCP/context seams.
- SB07 guards depend on all builders being out of runtime partials.

## Validation Depth

- Critical foundation.
- Requires Semantic Adequacy Gate proof.

## Implementation Steps

1. Extract context builder and providers first because it has fewer dependencies.
2. Extract skill builder and file skill policy behavior.
3. Extract tool builder and split configured workspace/provider diagnostic tool contributors if it reduces constructor size.
4. Extract MCP builder last, splitting secret binding, hosted MCP, local MCP, Playwright launch/cache, schema wrapping, and result compaction where tests need direct seams.
5. Register necessary services in `AddMafRuntimeArchitectureServices`.
6. Add direct tests and update runtime composer tests.

## Scope Exceptions

- Do not split every helper into an interface. Use concrete internal classes unless mocking is required.

## Do Not Do

- Do not create new `MafAgentRuntime.Capabilities.*` partial files.
- Do not leave any builder constructor accepting `MafAgentRuntime owner`.
- Do not move MCP secrets or environment binding into a broad utility without tests.

## Acceptance Checklist

- No private nested `*CapabilityBuilder` remains under `MafAgentRuntime`.
- No source match for `MafAgentRuntime owner` in extracted builders.
- Direct tests cover at least one positive and one negative path for each builder group.
- MCP secret binding tests prove denied/missing secrets fail predictably.

## Proof Required

- `proof/SB04/manifest.md`
- `proof/SB04/semantic-invariants.md`
- Build transcript.
- Focused builder unit test transcript.
- Source scan for `private sealed class .*Builder`, `MafAgentRuntime owner`, and `new *Builder(this)`.
- Anti-stub audit.

## Browser Validation Logging

- N/A unless Playwright MCP browser-visible diagnostics are changed.

## Progression Gate

- SB05 and SB06 may start only after all builders are top-level and the source scan returns no forbidden runtime-owned builders.

## Suggested Agent Prompt

```text
Implement SB04 only. Extract the hidden capability builders from MafAgentRuntime into named top-level components. Preserve behavior, split MCP by real testable responsibilities, and prove no builder remains runtime-owned.
```
