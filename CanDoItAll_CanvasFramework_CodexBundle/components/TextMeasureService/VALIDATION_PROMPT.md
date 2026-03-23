# Validation Prompt — TextMeasureService

Review the implementation of `TextMeasureService`.

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

- Truncation rules must be visually consistent across node cards, menus, chips, and calendar tiles.
- Selection/focus/hover/menu behavior stays coherent with the rest of the workbench/calendar family.
- Visual states are clear and consistent with the shared canvas language.

## Validate performance

- Cache by text/font/width tuple and avoid re-measuring during every pointer move or hover change.
- Hot-path work is batched or cached when relevant.
- No unnecessary full-surface refreshes or chatty interop loops were introduced.

## Validate accessibility

- Expose full text for tooltips and semantic mirrors when truncation occurs.
- Keyboard alternatives exist for critical actions when relevant.
- Non-color-only state communication is preserved for warnings/errors/selections.

## Validate tests

- Cover single-line, multi-line, emoji, long-word, and line-clamp edge cases.
- At least one regression test exists for the main behavior.
- Edge cases from the specification are covered proportionally to the component risk.

## Final review question

Would a future Codex agent recognize `TextMeasureService` as the one correct place to extend this behavior, or would they still be tempted to edit the old page/runtime code directly? If the answer is not clearly positive, the implementation is not architecturally finished.
