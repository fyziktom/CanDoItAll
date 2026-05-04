# MAF 1.3 Upgrade Contract

## Status

- `Completed`

## Objective

Upgrade the CanDoItAll MAF integration from `1.0.0` to the current stable `1.3.0` package line and document any source-level API remediation required before A2A/handoff implementation starts.

## Covered Inputs

- `NOTE-01`
- `NOTE-03`
- `NOTE-04`
- `REQ-01`

## Prerequisites

- Prepared bundle validation passed.
- Local MAF clone at `C:\repositories\agent-framework` remains available for API/sample comparison.
- NuGet package search confirms package versions as of 2026-05-02.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\CanDoItAll.AgentFramework.Core.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\CanDoItAll.AgentFramework.Maf.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\MafAgentRuntime.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\MafAgentRuntime.AgentFactory.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\MafAgentRuntime.Session.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Capabilities\MafAgentRuntime.Capabilities.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.A2A\A2AAgent.cs`
- `C:\repositories\agent-framework\dotnet\tests\Microsoft.Agents.AI.Workflows.UnitTests\Sample\12_HandOff_HostAsAgent.cs`

## Deliverables

- `Microsoft.Agents.AI`, `Microsoft.Agents.AI.OpenAI`, and `Microsoft.Agents.AI.Workflows` references updated to `1.3.0`.
- A package decision note for `Microsoft.Agents.AI.A2A` and hosting packages, including preview isolation rules.
- Source changes needed for MAF 1.3 API compatibility.
- Targeted build proof for Core and Maf projects.

## Dependency Impact

- Critical foundation for subbundles 03 and 04.
- If this build is weak, every later A2A/handoff phase can fail on stale API assumptions.

## Validation Depth

- Critical foundation.
- Package restore, targeted compile, and API compatibility proof.

## Implementation Steps

1. Update MAF package references to `1.3.0`.
2. Add A2A package references only if code in subbundle 03 needs them; otherwise record the intended preview versions.
3. Run restore/build for Core and Maf.
4. Fix compile errors with minimal adapter changes.
5. Record any API deltas that affect A2A/handoff subbundles.

## Scope Exceptions

- Do not implement A2A registry or handoff flow in this subbundle.
- Do not convert the solution to Central Package Management unless package update friction proves that necessary and an architecture gate approves it.

## Do Not Do

- Do not leak preview A2A types into public Core contracts.
- Do not update unrelated packages only because NuGet reports newer versions.
- Do not suppress new warnings unless they are known false positives and documented.

## Acceptance Checklist

- Stable MAF packages are at `1.3.0`.
- Core and Maf build against the new package graph.
- Any preview A2A dependency is isolated to adapter/hosting projects.
- API deltas are documented for later subbundles.

## Proof Required

- `dotnet restore CanDoItAll.slnx`
- `dotnet build src/CanDoItAll.AgentFramework.Core/CanDoItAll.AgentFramework.Core.csproj --no-restore -m:1`
- `dotnet build src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj --no-restore -m:1`
- `dotnet list src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj package`
- Completed on 2026-05-02. Restore and targeted builds passed. The first parallel Maf build hit a transient CS2012 file lock against the shared Models assembly; a sequential rerun passed. Existing NU1902/NU1904 package advisories remain out of this subbundle scope.

## Browser Validation Logging

- N/A. This subbundle does not affect browser-visible UI.

## Progression Gate

- Downstream A2A/handoff implementation may continue only after Maf builds or after an explicit remediation subbundle is inserted.

## Suggested Agent Prompt

```text
Implement subbundle 01 only: update MAF stable packages to 1.3.0, resolve compile/API changes in the MAF adapter, and prove Core/Maf build. Do not add A2A or process-flow behavior yet except for package references required to compile.
```
