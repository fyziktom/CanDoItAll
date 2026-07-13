# Dependency Direction Inventory

## Current Observations From Preparation

- `src/Processes/*` project references point to Processes, Drivers.Abstractions, Foundation, and Git dependencies. No project-reference evidence showed `src/Processes/*` referencing MAF projects.
- `src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs` contains AgentFramework wording in user-facing projection summaries. This is not a project dependency, but SB01 must decide whether these labels remain acceptable display text or become driver-neutral observation labels.
- `src/Modules/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj` references MAF and `CanDoItAll.Modules.AgentFramework`. This is currently where the giant integration file lives. The bundle target is to move AgentFramework/MAF runtime execution policy below the Processes driver boundary, not into `src/Processes/*`.
- `src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj` references many MAF implementation projects. An MAF-owned process driver may need to live near this layer or in an MAF common project.
- `src/Processes/CanDoItAll.Processes.Runtime/ProcessStrategyDispatcher.cs` already invokes strategy factories from `CanDoItAll.Processes.Drivers.Abstractions`; this is the correct generic-to-driver invocation direction but needs richer driver ports for prompt/evidence/step dispatch policy.

## Required Static Checks During Execution

Run and capture transcripts:

```powershell
rg -n "ProjectReference Include=.*(MAF|AgentFramework|Modules.AgentFramework)" src\Processes -g "*.csproj"
rg -n "using CanDoItAll\.AgentFramework|CanDoItAll\.Modules\.AgentFramework|namespace .*AgentFramework" src\Processes -g "*.cs"
rg -n "CanDoItAll\.AgentFramework|Modules\.AgentFramework|MAF" src\Processes -g "*.cs" -g "*.csproj"
```

Expected result:

- No project references from `src/Processes/*` to MAF or AgentFramework.
- No code-level `using` dependency from `src/Processes/*` to MAF or AgentFramework.
- Any remaining display text that says `AgentFramework` is explicitly reviewed and either moved behind driver observation labels or documented as user-facing legacy terminology without type dependency.

## Driver Placement Candidates

Preferred:

- `src/MAF/Common/CanDoItAll.AgentFramework.Processes.Driver`
- `src/MAF/Processes/CanDoItAll.AgentFramework.Processes.Driver`

Allowed transitional composition shim:

- `src/Modules/CanDoItAll.Modules.Processes/Drivers/AgentFramework` only if it contains composition glue and all driver behavior still depends on Processes abstractions from below. This must be treated as temporary and cannot leak MAF dependencies into `src/Processes/*`.

Rejected:

- `src/Processes/Drivers/CanDoItAll.Processes.Drivers.AgentFramework` if it references MAF.
- Adding AgentFramework references to `CanDoItAll.Processes.Application`, `Runtime`, `Builder`, `Core`, `Templates`, or `Drivers.Standard`.
