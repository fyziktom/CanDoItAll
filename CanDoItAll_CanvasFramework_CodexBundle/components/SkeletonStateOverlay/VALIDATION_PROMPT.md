# Validation Prompt — SkeletonStateOverlay

Review the implementation of `SkeletonStateOverlay`.

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

- Loading states should preserve layout rhythm so the scene does not jump when content appears.
- Selection/focus/hover/menu behavior stays coherent with the rest of the workbench/calendar family.
- Visual states are clear and consistent with the shared canvas language.

## Validate performance

- Keep skeleton DOM simple and respect reduced-motion preferences.
- Hot-path work is batched or cached when relevant.
- No unnecessary full-surface refreshes or chatty interop loops were introduced.

## Validate accessibility

- Loading regions should announce busy state without spamming screen readers.
- Keyboard alternatives exist for critical actions when relevant.
- Non-color-only state communication is preserved for warnings/errors/selections.

## Validate tests

- Add snapshot tests for loading variants and reduced-motion behavior.
- At least one regression test exists for the main behavior.
- Edge cases from the specification are covered proportionally to the component risk.

## Final review question

Would a future Codex agent recognize `SkeletonStateOverlay` as the one correct place to extend this behavior, or would they still be tempted to edit the old page/runtime code directly? If the answer is not clearly positive, the implementation is not architecturally finished.
