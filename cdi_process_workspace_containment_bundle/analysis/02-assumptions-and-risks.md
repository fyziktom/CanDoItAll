# Assumptions And Risks

## Assumptions

- The processes workspace should keep its current information architecture and only gain bounded-height behavior.
- The modal should continue using the existing fullscreen dialog and `ListDetailShell` rather than switching to a different modal layout primitive.
- The Mermaid preview may clip overflow inside its viewport as long as zoom and pan remain usable inside that viewport.

## Critical Path Risks

- The page-shell containment change is a critical UI foundation. If the `PageScaffold` and `Tabs` height contract is wrong, later modal validation is weak because the same regression pattern can still exist in the main workspace.
- The Mermaid containment change is a critical UI foundation for the modal proof. If the preview host still leaks transformed content, screenshots may look better at one viewport while still being wrong during interaction.

## Validation Risks

- Component tests can confirm class and structure changes, but they cannot prove real browser overflow and clipping behavior.
- The templates dialog regression is screenshot-driven, so Playwright proof must include the open modal state, the diagrams tab, and an explicit zoom interaction before closure.
- Existing process bundle tests cover the library flow already; if they are brittle around local data seeding, browser proof must still be captured and documented even if a new assertion needs small stabilization work.

## Reopen Triggers

- Reopen subbundle 01 if the `/processes` page still scrolls at the document level because the list or tab pane height contract is incomplete.
- Reopen subbundle 02 if the templates dialog body still shows nested scrolling or if the Mermaid preview can be seen outside its own preview card after zoom or pan.
- Reopen any earlier subbundle if browser screenshots reveal clipping, overlap, unreadable content, or lost access to existing actions in the process workspace.
