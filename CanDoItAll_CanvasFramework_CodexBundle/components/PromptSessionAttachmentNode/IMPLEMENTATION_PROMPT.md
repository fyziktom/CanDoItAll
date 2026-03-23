# Implementation Prompt — PromptSessionAttachmentNode

Implement `PromptSessionAttachmentNode` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Prompt Factory domain components`
- Status today: `partial`
- Priority: `P1`
- Scope: `domain-specific`
- JS bridge: `partial`

## Objective

Shared card variant and adapter logic for attachment/media nodes inside the prompt session graph.

## Required behavior

- Render image or file attachments on the Prompt Factory canvas.
- Reuse the same attachment node pattern for future asset-heavy project canvases.
- Support preview, status badges, and future inline actions.

## Reuse / refactor directives

- Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.Modules.Factory/FactoryDomain.cs#L359-L536`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309`

## Recommended implementation locations

- `src/CanDoItAll.Modules.Factory/CanvasAdapters/PromptSessionAttachmentNode.cs`
- `src/CanDoItAll.Modules.Factory/wwwroot/js/prompt-session-attachment-node.js`
- `tests/CanDoItAll.Tests.Components/PromptSessionAttachmentNodeTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `PromptSessionAttachmentNode` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `PromptSessionAttachmentNode` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
