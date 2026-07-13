# C# Dependency Direction

## Current Project References

`CanDoItAll.AgentFramework.Maf` currently references:

- `CanDoItAll.AgentFramework.Models`
- `CanDoItAll.AgentFramework.Core`
- `CanDoItAll.AgentFramework.WorkflowExecutors.Core`
- `CanDoItAll.AgentFramework.Workflows.MafAdapter`
- `CanDoItAll.AgentFramework.Providers`
- `CanDoItAll.AgentFramework.Tooling`
- `CanDoItAll.AgentFramework.Capabilities.Abstractions`
- `CanDoItAll.AgentFramework.Capabilities.Access`
- `CanDoItAll.AgentFramework.Mcp.Abstractions`
- `CanDoItAll.AgentFramework.Mcp`
- `CanDoItAll.AgentFramework.Skills.Abstractions`
- `CanDoItAll.AgentFramework.Skills`
- `CanDoItAll.AgentFramework.Tools.Abstractions`
- `CanDoItAll.AgentFramework.Tools`
- `CanDoItAll.Tools.Documents`
- `CanDoItAll.Modules.Security`
- `CanDoItAll.Modules.Workspace`
- `CanDoItAll.SharedKernel`

The scoped CodeAnalytics dependency result for `snap-20260706180906-6ece4834` reported no cycles inside the scoped MAF snapshot. The snapshot scope did not fully expand the external project-reference graph, so SB07 must rerun dependency proof if project references change.

## Target Direction

```text
Composition modules / hosting
  -> CanDoItAll.AgentFramework.Maf
  -> AgentFramework Core / Models / Abstractions

CanDoItAll.AgentFramework.Maf runtime implementations
  -> Core, Models, Providers, Tooling, Capabilities, Mcp, Skills, Tools

Abstractions / Core / Models
  must not reference Maf implementation types
```

## Forbidden References

- `CanDoItAll.AgentFramework.Core` must not reference `CanDoItAll.AgentFramework.Maf`.
- Capability abstractions must not reference MAF implementation.
- Tool abstractions must not reference MAF implementation.
- New workspace tool contracts must not depend on `Microsoft.Agents.AI` unless they are explicitly MAF adapter contracts.
- Do not solve cycles by moving unrelated code into `Common` or `SharedKernel`.

## New Contract Projects Needed

None planned at preparation time. SB07 may add a small abstractions project only if:

- a new implementation project is required for workspace tool families or provider adapters,
- shared contracts would otherwise create a cycle,
- or tests need to reference contracts without pulling provider SDK implementation packages.

## Build And Test Proof Required

- Before any project-reference change: capture `.csproj` reference table.
- After any project-reference change: run `dotnet build` for affected projects and a focused unit test slice.
- Refresh CodeAnalytics or record explicit MCP unavailability.
- Record why each new reference is necessary.

## Implementation Update - 2026-07-06

- No project references were added during the partial implementation pass.
- Final scoped CodeAnalytics snapshot: `snap-20260706191451-275f822a`.
- Final dependency query for cycles returned `cycles: []`.
- The new extracted owners remain inside `CanDoItAll.AgentFramework.Maf`, preserving the planned direction:
  - `MafApprovalContinuationDriver`
  - `MafRuntimeSessionPersistenceDriver`
  - `MafRuntimeResponseAssembler`
  - `MafScriptPolicyInspectionService`
  - `RuntimeCapabilityAccessPlanner`
  - `RuntimeCapabilityDescriptorCatalog`
  - `RuntimeRegisteredToolProviderAttacher`
  - `ConfiguredWorkspaceToolSet`
  - `WorkspaceImageAnalysisModelResolver`
