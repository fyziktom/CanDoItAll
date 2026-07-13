# C# Dependency Direction

## Current Project References

CodeAnalytics snapshot `snap-20260707234748-ac72a0ea` confirmed:

- `CanDoItAll.AgentFramework.Maf` references MAF core, models, providers, skills, tools, MCP, workflow executor core, `Workflows.MafAdapter`, selected modules, shared kernel, and document tools.
- `CanDoItAll.AgentFramework.Workflows.MafAdapter` references core, models, workflow executor abstractions/core/standard, workflow core, and workflow runtime.
- `CanDoItAll.AgentFramework.Hosting` references MAF, persistence, voice, workflow core/runtime/adapter, and hosting A2A package.
- `CanDoItAll.Modules.AgentFramework`, `CanDoItAll.Modules.Processes`, and tests reference the MAF/workflow adapter surfaces.

## Target Project References

- Package update should not require new project references.
- If a new reference is proposed, `SB04` must block execution until it records before/after project reference table, direction justification, cycle check, build proof, and replacement options considered.

## Forbidden References

- `CanDoItAll.Processes.*` -> `Microsoft.Agents.AI*`
- `CanDoItAll.Processes.*` -> `CanDoItAll.AgentFramework.Maf`
- `CanDoItAll.AgentFramework.Core` -> MAF adapter implementation package APIs
- `CanDoItAll.AgentFramework.Models` -> MAF adapter implementation package APIs
- MAF adapter projects -> process module implementation details
- Any core/abstraction project -> provider SDK package details introduced by this update

## Cycle Risk

- CodeAnalytics dependency query reported module/type cycles in `CanDoItAll.Modules.AgentFramework` node ids during preparation. No implementation changes are planned there, but `SB04` must rerun dependency checks or source review if any changed file touches module references.
- Any new project reference must be treated as an architecture change and requires bundle repair before implementation continues.

## New Contract Projects Needed

None planned.

If implementation discovers a contract extraction is required, this conservative package-update bundle is no longer sufficient. Repair the bundle and run architecture readiness again before continuing.

## Build And Test Proof Required

- `dotnet restore CanDoItAll.slnx`
- `dotnet build CanDoItAll.slnx --configuration Release --no-restore`
- Focused unit/integration tests listed in `SB05`
- Source scan for forbidden references and process-provider introduction
- CodeAnalytics before/after or documented unavailability if project references change
