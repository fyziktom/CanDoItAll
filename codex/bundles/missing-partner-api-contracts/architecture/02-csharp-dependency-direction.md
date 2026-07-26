# C# Dependency Direction

## Current Product Direction

```text
CanDoItAll.Web
  -> Modules.AgentFramework / Modules.CrmHr
  -> AgentFramework Core / Workflows Core
  -> AgentFramework Models / Workflows Abstractions

AgentFramework.Persistence
  -> AgentFramework Core / Models / Workflows Core
```

## Target Direction

- Preserve the current inward direction.
- New public HTTP DTOs stay in Web unless another non-Web client requires a contract
  assembly during implementation.
- Core portable contracts contain no ASP.NET, EF, provider SDK, or CRM-HR types.
- Agent recruiting target discriminators use stable primitive ids and do not reference
  Workflow/Process runtime implementation types.
- Persistence implements Core abstractions; Core never references Persistence.

## Forbidden References

- AgentFramework Models/Core -> Web.
- AgentFramework Models/Core -> Modules.CrmHr.
- Workflows Abstractions/Core -> Web.
- Processes.Contracts/Core -> CRM-HR or Web for this work.
- SharedInfo -> local product source by filesystem link.

## Cycle Gate

- Before/after scoped CodeAnalytics dependency proof is required if any `.csproj` changes.
- Any new project cycle reopens the responsible subbundle and blocks SB07/SB08.
- If a desired type reference would create a cycle, replace it with a primitive typed
  discriminator contract in the lower-level owner.
