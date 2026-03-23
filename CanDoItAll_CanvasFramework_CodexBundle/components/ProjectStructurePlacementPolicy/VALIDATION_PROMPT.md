# Validation Prompt — ProjectStructurePlacementPolicy

Review the implementation of `ProjectStructurePlacementPolicy`.

## Validate function

- The component satisfies the minimum viable version described in the specification.
- The component handles read-only, disabled, and error fallback states correctly.
- The declared callbacks/events are wired and typed correctly.

## Validate architecture

- The implementation respects the shared vs domain-specific boundary.
- No duplicate parallel abstraction was introduced.
- Old page-local or legacy ownership was reduced or removed.
- Business logic remains in C# unless the work is inherently JS/browser specific.

## Validate UX/UI

- Predictable placement reduces the need for immediate cleanup drag operations.
- Selection/focus/hover/menu behavior stays coherent with the rest of the workbench/calendar family.
- Visual states are clear and consistent with the shared canvas language.

## Validate performance

- Placement policy should use cached layout bounds rather than forcing fresh measurements.
- Hot-path work is batched or cached when relevant.
- No unnecessary full-surface refreshes or chatty interop loops were introduced.

## Validate accessibility

- Expose resulting placement coordinates or selection results through inspector feedback when relevant.
- Keyboard alternatives exist for critical actions when relevant.
- Non-color-only state communication is preserved for warnings/errors/selections.

## Validate tests

- Cover child, sibling, canvas-root, and collision scenarios.
- At least one regression test exists for the main behavior.
- Edge cases from the specification are covered proportionally to the component risk.

## Final review question

Would a future Codex agent recognize `ProjectStructurePlacementPolicy` as the one correct place to extend this behavior, or would they still be tempted to edit the old page/runtime code directly? If the answer is not clearly positive, the implementation is not architecturally finished.
