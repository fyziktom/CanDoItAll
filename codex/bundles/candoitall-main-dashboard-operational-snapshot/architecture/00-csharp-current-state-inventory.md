# C# Current-State Inventory

## Evidence Status

- CodeAnalytics: unavailable; no snapshot/findings/cycle evidence exists.
- Fallback evidence: direct inspection of the exact `repo://` files named below and project/DI registrations.

## Inspected Sources And Responsibilities

| Source | Size / dependencies | Current responsibility | Architecture response |
| --- | --- | --- | --- |
| `repo://src/App/CanDoItAll.Web/Components/Pages/Home.razor` | 203 lines; 3 injected services | Workbench summaries, project count, quick navigation, UI | Replace broad sources with one snapshot service; retain UI/timer orchestration. |
| `repo://src/Modules/CanDoItAll.Modules.Projects/ProjectModels.cs` | 1006 lines; `ProjectsService` has 6 ctor dependencies | Project writes plus broad hierarchy/portfolio list mapping | New top-level recent-project query; do not grow `ProjectsService`. |
| `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Core/WorkflowOverviewQueryService.cs` | 122 lines; 3 ctor dependencies | Aggregate workflow overview | Leave unchanged; dedicated dashboard query/store path. |
| `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs` | 2598 lines; run store uses context factory | Workflow run/checkpoint/event/artifact persistence and overview store | Add only the narrow interface implementation method; no new partial. |
| `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs` | 1907 lines; 6 ctor inputs (3 optional) | Full/list/detail process projections and enrichment | New cohesive top-level dashboard query; do not grow this service. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/AgentFrameworkWorkspaceService.cs` | 280-line main file plus 2 partial facade files; up to 15 ctor inputs | Broad AgentFramework catalog/execution facade | New top-level usage-totals query; adding another partial is forbidden. |
| `repo://src/Foundation/CanDoItAll.Infrastructure/ControlPlane/CanonicalRuntimeDatabase.cs` | singleton, 2 ctor dependencies | Canonical runtime identity | Read `IDatabaseRuntimeState.GetSnapshot()` profile ID, fingerprint, and generation for cache identity. Switching is restart-only today and generation remains 0. |

## Direct Instantiation And Composition

- Workflow overview and process query services are directly instantiated in existing unit tests; new query services need equally direct isolated tests.
- `AgentFrameworkWorkspaceService` is directly constructed by its factory and several tests; dashboard work must not expand its constructor.
- Module DI files are the production composition roots: Projects module extensions, Workflow Core/runtime plus AgentFramework module overrides, Processes module extensions, and Web `Program.cs`.

## Current Tests

- Workflow overview: `repo://tests/Unit/CanDoItAll.Tests.Unit/WorkflowOverviewQueryServiceTests.cs`.
- Process projection: `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs`.
- Agent usage: `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests.cs`.
- Home-specific tests: missing despite the page capsule claim.
- Cache/timer/profile-generation tests: missing.

## Risk Notes

- Existing large files make “just add a method” attractive but would concentrate responsibility.
- Process projection status is inside JSON, so the existing bounded candidate window cannot be the source of truth for active selection. Canonical runtime state has typed status/update fields and must select IDs first.
- `ICanonicalRuntimeDatabase.Generation` is currently constant 0 and database switching requires restart; profile ID remains part of the key and tests must vary both fields.
- Agent, workflow, and process usage may overlap. Only the Agent usage projection totals are presented.
