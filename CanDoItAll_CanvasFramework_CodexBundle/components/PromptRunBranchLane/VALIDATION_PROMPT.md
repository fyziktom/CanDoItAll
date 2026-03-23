# Validation Prompt — PromptRunBranchLane

Review the implementation of `PromptRunBranchLane`.

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

- Lanes should clarify branching without making the canvas feel rigid or over-segmented.
- Selection/focus/hover/menu behavior stays coherent with the rest of the workbench/calendar family.
- Visual states are clear and consistent with the shared canvas language.

## Validate performance

- Treat lanes as lightweight grouping metadata rather than heavy nested DOM trees.
- Hot-path work is batched or cached when relevant.
- No unnecessary full-surface refreshes or chatty interop loops were introduced.

## Validate accessibility

- Expose branch names and order in semantic summaries and inspector lists.
- Keyboard alternatives exist for critical actions when relevant.
- Non-color-only state communication is preserved for warnings/errors/selections.

## Validate tests

- Cover lane bounds, ordering, and branch-label rendering.
- At least one regression test exists for the main behavior.
- Edge cases from the specification are covered proportionally to the component risk.

## Final review question

Would a future Codex agent recognize `PromptRunBranchLane` as the one correct place to extend this behavior, or would they still be tempted to edit the old page/runtime code directly? If the answer is not clearly positive, the implementation is not architecturally finished.
