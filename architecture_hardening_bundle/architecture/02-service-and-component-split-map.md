# Service and component split map

## Current concentration points

### Service side
- `ProcessesService.Persistence.cs`
- `ProcessesService.Publication.cs`
- `ProcessesService.Runtime.cs`
- `ProcessesService.Reads.cs`
- `ProcessesService.Support.cs`

### UI side
- `Components/ProcessWorkspace.razor`
- `Components/ProcessWorkspace.razor.cs`
- `Components/ProcessWorkspace.Canvas.*.cs`

## Recommended split map

| Current concentration | Responsibility that should emerge | Notes |
| --- | --- | --- |
| Definition save logic | Definition persistence / diff engine | Keep public entrypoint stable if useful |
| Slug/version helpers | Publication support / shared slug helper | Avoid duplicate rules between modules |
| Publish lifecycle + clone | Publication service + clone engine | Separate lifecycle decision from graph cloning |
| Transition orchestration | Runtime command service + policy/planner helpers | Preserve public API surface |
| List/detail/analytics reads | Query services | Projection-only, no shadow mutation |
| Template parsing and summaries | Template domain helpers + shared file/json parsing where appropriate | Separate generic and domain-specific extraction |
| Process workspace shell | Smaller components + state container/presenter | UI should consume prepared state |
| Canvas mutation helpers | Focused canvas state/action services | Avoid domain rules inside markup |

## Split sequencing rule

Do not perform the big UI split before the canonical model and persistence core are stable enough. Otherwise the UI refactor will be forced to compensate for unstable internals.

## Anti-goal

Do not create:
- a `ProcessesCoordinator` that is just another god class,
- a workspace “state manager” that contains all domain logic,
- query services that start mutating definitions or runtime state.
