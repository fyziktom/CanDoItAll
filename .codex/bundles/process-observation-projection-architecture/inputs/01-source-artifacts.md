# Source Artifacts

## Skills And MCP Sources

- `C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\SKILL.md`
- `C:\Users\lucys\.codex\skills\analyzing-dotnet-performance\SKILL.md`
- `C:\Users\lucys\.codex\skills\aspnet-core\SKILL.md`
- Microsoft Learn MCP search and fetch results from 2026-05-08.

## Microsoft Learn Guidance Used

- [ASP.NET Core Blazor rendering performance best practices](https://learn.microsoft.com/aspnet/core/blazor/performance/rendering?view=aspnetcore-10.0)
- [ASP.NET Core Razor component virtualization](https://learn.microsoft.com/aspnet/core/blazor/components/virtualization?view=aspnetcore-10.0)
- [ASP.NET Core Blazor state management overview](https://learn.microsoft.com/aspnet/core/blazor/state-management/?view=aspnetcore-10.0)
- [ASP.NET Core Blazor cascading values and parameters](https://learn.microsoft.com/aspnet/core/blazor/components/cascading-values-and-parameters?view=aspnetcore-10.0)
- [Cache in-memory in ASP.NET Core](https://learn.microsoft.com/aspnet/core/performance/caching/memory?view=aspnetcore-10.0)
- [Overview of ASP.NET Core SignalR](https://learn.microsoft.com/aspnet/core/signalr/introduction?view=aspnetcore-10.0)
- [Host ASP.NET Core SignalR in background services](https://learn.microsoft.com/aspnet/core/signalr/background-services?view=aspnetcore-10.0)

## Code Analytics

- Snapshot: `snap-20260508224200-0d8ff021`
- Solution: `C:\repositories\CanDoItAll\CanDoItAll.slnx`
- Scope: `CanDoItAll.Modules.Processes`
- Snapshot facts: 1 scoped project, 152 source documents, target framework `net10.0`.
- Notable findings from the snapshot:
  - `ProcessWorkspace` exposes 510 source members across partials.
  - `ProcessesService` exposes 266 source members across partials.
  - `ProcessWorkspaceRunDetailsLoader` exposes 33 source members.
  - A type cycle exists among `ProcessWorkspace` and its nested presenter types.
  - DI collector ambiguity exists at `src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` line 41, but this is a tooling ambiguity around factory registration, not a runtime finding.

## Existing Process/UI Performance Context

- `.codex/bundles/process-runtime-ui-performance`
  - Existing prior optimization: active-run summary avoided repeated full-detail reads.
  - Existing prior proof recorded `LoadActiveRunSummariesAsync` around 60 ms for 12 active runs after optimization.
  - Existing prior UI behavior: Runs tab refresh avoids analytics reload unless analytics is active.
- `.codex/bundles/process-runtime-execution-performance-review`
  - Existing prior optimization: runtime start paths pre-index step role requirements, artifact expectation titles, and effective assignments.
  - Existing prior validation included mock-agent workflow and independent simple .NET app builds.

## Main Source Files Mapped

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Pages\ProcessesPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.LiveRefresh.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Loading.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.RunsPresenter.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunDetailsLoader.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsTab.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsActiveSection.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.RuntimeReadQuery.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.RuntimeReadQuery.Support.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessRuntimeStateOverviewService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessOperatorControlPlane.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessOutbox.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services\ProcessesService.Reads.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services\ProcessesModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\CanDoItAll.Modules.Processes.csproj`

## UI Library Facts

- Processes module references `CanDoItAll.Components.BaseLib` and `CanDoItAll.Components.CanvasLib`.
- No `Radzen` package reference was found in `.csproj` files under `src`, `tests`, or `tools`.
- Tailwind infrastructure exists in the repository, and the current Processes page already uses Tailwind utility classes together with BaseLib wrappers.
