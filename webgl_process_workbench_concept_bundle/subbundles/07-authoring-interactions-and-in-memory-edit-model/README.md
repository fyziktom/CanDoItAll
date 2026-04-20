# Authoring interactions and in-memory edit model

## Status

- Completed

## Objective

- Make the sandbox concept interactive through in-memory node movement, connection changes, selection/inspector context, and resettable command history.

## Covered Inputs

- `IN-10`
- `IN-11`
- `RQ-08`
- `RQ-14`
- `RQ-16`

## Prerequisites

- `06-dedicated-webgl-sandbox-and-template-switching`

## Exact Source References

- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.Actions.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.Links.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.Persistence.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/ProcessCanvasSurfaceFactory.cs
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Components/ProcessCanvasSurfaceFactoryTests.cs

## Deliverables

- In-memory sandbox state holder for the selected template and its unsaved edits.
- Node-drag, connect/disconnect, selection, reset, and lightweight inspector/event-log flows.
- Focused tests proving semantic changes update the sandbox scene model.

## Dependency Impact

- Gate B depends on these interactions proving authoring value rather than view-only novelty.
- Automation work depends on the interaction semantics being explicit and deterministic.

## Validation Depth

- High
- Focused tests + mandatory browser proof for move/connect flows

## Implementation Steps

1. Add an in-memory scene/document state holder to the sandbox project.
2. Wire WebGL runtime events back into the state holder for move/select/connect/disconnect flows.
3. Mirror the current process editor semantics where appropriate, but keep everything in-memory and sandbox-only.
4. Add reset and last-command visibility so interactions stay reviewable.


## Do Not Do

- Do not persist sandbox edits into the real Processes module.
- Do not bypass semantic events with hidden direct state mutation.
- Do not add overly complex undo/redo if a simple reset plus last-command log is sufficient for the concept.

## Acceptance Checklist

- A reviewer can move a node and modify at least one connection in the sandbox.
- The in-memory model updates consistently and can be reset.
- Focused tests and browser proof cover the touched interaction paths.

## Proof Required

- Run focused component/integration tests for the sandbox state holder and interaction adapters.
- Capture before/after screenshots for a node move and a connection mutation.
- Record the resulting scene snapshot deltas or event-log output.
- Validation commands to run for this subbundle:
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~WebGl|FullyQualifiedName~ProcessWorkspace" -v:minimal`

## Browser Validation Logging

- Route: dedicated WebGL sandbox route.
- Actions: move a node, connect or disconnect an edge, inspect selection metadata, reset the scene.
- Screenshots: capture before and after the interaction on at least one medium or dense template.
- Review questions: do interactions feel semantically meaningful, do labels remain readable during/after movement, and is reset reliable?

## Progression Gate

- Gate B may only run after node move and connection mutation work semantically and remain isolated to the sandbox's in-memory model.

## Suggested Agent Prompt

```text
Implement only subbundle 07. Add the in-memory sandbox edit model, wire node move/connect/disconnect/select interactions through semantic events, prove reset works, update focused tests, capture browser proof, and stop before the next architecture gate.
```

## Preserved Bundle Notes

### Review questions

- Does the concept now prove real authoring value rather than just camera novelty?
- Are sandbox edits still clearly isolated from production persistence?
- Are move/connect flows explicit enough for later automation hooks?

### Validation commands

- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~WebGl|FullyQualifiedName~ProcessWorkspace" -v:minimal`

### Corrective trigger

- If this subbundle fails, open `_corrective-scene-contract-and-layout-reset` before continuing downstream.

### Corrective template

- `subbundles/_corrective-scene-contract-and-layout-reset`

### Repository touchpoints (relative)

- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.cs`
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.Actions.cs`
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.Links.cs`
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.Persistence.cs`
- `src/CanDoItAll.Modules.Processes/ProcessCanvasSurfaceFactory.cs`
- `tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs`
- `tests/CanDoItAll.Tests.Components/ProcessCanvasSurfaceFactoryTests.cs`

### Notes

- Treat this subbundle as an isolated execution slice. Do not continue into later numbered work during the same pass.
- Update `reviews/01-execution-report.md` and `reviews/02-architecture-gate-memo-log.md` as soon as this subbundle either passes, blocks, or triggers a corrective path.
