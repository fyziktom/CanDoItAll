# 02-canonical-port-model-and-persistence-foundation

## Status

- `Completed`

## Objective

- Ensure every process-canvas relationship that later UI phases will claim to edit has a real canonical home, real save behavior, and real reload behavior, including any required model extension for artifact consumption.

## Covered Inputs

- `R012` Decision authority remains singular on the target side.
- `R015` Decide whether artifact expectations require explicit graph links.
- `R016` Extend canonical storage if explicit artifact consumption is needed.
- `R018` Every authored relation and move must round-trip through save and reload.
- `R019` Call out what is already canonical versus what needs extension.
- `R020` Do not claim success for transient UI-only features.

## Prerequisites

- `subbundles/01-node-inventory-and-port-semantics` must be `Completed` and trusted.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.PostgreSql`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.Sqlite`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration`
- `C:\repositories\CanDoItAll\cdi_process_canvas_full_authoring_bundle\analysis\03-architecture-troubles-log.md`

## Deliverables

- Canonical-model and service changes required for the generalized process-canvas graph.
- Migrations for any new persistable relationship such as artifact consumption.
- Integration tests proving save, reload, and projection rebuild for every newly authored relationship family in scope.
- Explicit resolution for step and branch derived-node position round-tripping where the new work affects it.

## Dependency Impact

- Every later UI phase depends on this phase because the canvas must not advertise relationships the service layer cannot persist.
- If this phase is weak, role links, artifact links, or step links may appear to work in-browser and then disappear after save or reload.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Map each port family from the typed catalog to its canonical entity or field.
2. Extend the canonical model and service layer for any in-scope relation that lacks persistence, especially artifact consumption if required.
3. Add database migrations where the model changes.
4. Update editor and projection round-trip paths so authored links and moved nodes survive save and reload.
5. Add focused integration tests for round-trip persistence and projection rebuild.

## Scope Exceptions

- This phase does not need to ship final browser-visible node rendering.
- If artifact consumption is intentionally deferred, it must be written as an explicit blocked exception with the exact missing model and the exact reason; do not hide it as future polish.

## Do Not Do

- Do not draw or accept a new canvas relationship without a canonical save path.
- Do not encode port semantics only in JavaScript or only in the Blazor component.
- Do not silently relax singular decision-authority semantics.

## Acceptance Checklist

- Every in-scope authored relationship family has a canonical mapping.
- Any required model extension is implemented with migrations and service updates.
- Save and reload round-trips preserve newly authored relationships.
- Projection rebuilds after later interactions do not snap nodes or links back to stale state.

## Proof Required

- Focused integration-test command covering persistence and projection rebuild.
- Migration presence where the canonical model changes.
- Service-layer proof that newly authored links survive re-fetch through the editor model.

## Browser Validation Logging

- Route: `/processes`
- Viewport: `Maximized desktop`
- Playwright MCP actions: `Optional smoke only if persistence changes are already browser-exercisable in this phase`
- Screenshot paths: `Only if a live smoke is needed to confirm a persistence fix`
- Review focus: `Reload and verify that authored links and moved nodes do not snap back`

## Progression Gate

- Downstream UI subbundles may continue only after integration tests prove round-trip persistence for every authored relation family already put into scope, and any unresolved relation family is documented explicitly as blocked rather than left ambiguous.

## Suggested Agent Prompt

```text
Implement only subbundle 02 from C:\repositories\CanDoItAll\cdi_process_canvas_full_authoring_bundle. Map every in-scope process-canvas port family to a canonical persisted relation, extend the model and service layer where the current process model is too weak, add migrations if needed, add focused integration tests for save and reload round-trips, and do not move on until the canvas semantics are honest.
```
