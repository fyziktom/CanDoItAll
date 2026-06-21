# Assumptions And Risks

## Assumptions

- Simple notes are Workbench `ProjectObjectType.Note` nodes with empty subtitle, mapped to CanvasLib inline text nodes.
- The correct persisted contract is full note body in `ProjectObjectRecord.Notes`; display title is derived from the first non-empty line and bounded for the `Title` column.
- Updating the CanvasLib package in `repo://ExternalPackages` is acceptable if the shared component source is rebuilt from the local `C:/repositories/CanDoItAll.Components` workspace and the CanDoItAll package reference is updated consistently.
- The existing unrelated dirty change in `C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.BaseLib/Components/Modals/Dialog.razor` is not part of this bundle and must not be reverted or claimed.

## Critical Path Risks

- `SB01` is a critical foundation. If long note bodies are still not preserved, `SB02` visual proof is untrustworthy because it may render text that will not survive save/reload.
- `SB02` depends on `SB01` because layout proof must use persisted/runtime note text, not only a seeded DOM string.
- Rebuilding a local package from a sibling source workspace can drift if package references are not updated or if NuGet cache keeps serving the old package.

## Validation Risks

- Browser proof depends on a runnable CanDoItAll web app and Playwright access to the project-structure route.
- The exact user failure is intermittent and not fully specified; proof must include adversarial long, multiline, punctuation-heavy text and persisted state checks rather than only a happy-path short note.
- Canvas screenshots alone can hide storage defects. Component/integration assertions must check persisted `Notes`.
- Package proof must confirm the app is using the rebuilt package/assets, not the stale NuGet cache.

## Reopen Triggers

- Reopen `SB01` if any long-note create or edit path stores only a shortened title, first line, or stale textarea value in `Notes`.
- Reopen `SB01` if browser runtime state exposes long text but a reload or service query loses it.
- Reopen `SB02` if the rendered DOM width does not match the measured inline-node width closely enough to use available space.
- Reopen `SB02` if screenshot review finds note text clipped, overlapping badges, unreadable wrapping, or card/hitbox mismatch.
