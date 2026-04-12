# Structured Input

## Core Objective

- Use the shared components MCP and the AgentFramework sandbox Chat page as the reference containment pattern, then apply that fit-to-window layout behavior to the `/processes` workspace and the fullscreen templates modal.

## Hard Constraints

- Stay within the existing processes module and BaseLib component model.
- Prefer the smallest correct change over a structural rewrite.
- Reuse shared layout primitives instead of replacing them with one-off wrapper patterns.
- Browser proof is required because the trigger artifact is a rendered overflow regression.

## Source Artifacts

- `C:\repositories\CanDoItAll.AgentFramework\src\CanDoItAll.AgentFramework.Sandbox\Components\Pages\Chat.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessTemplateLibraryDialog.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessTemplateMermaidPreview.razor`
- Attached screenshot in the Codex thread on 2026-04-12

## Input Coverage Signals

- `Make Process definitions cards list scrollable inside, same for content of tabs.`
- `Same for the modal with Templates. List must be scrollable same as content.`
- `You must assure that mermaid graph during zoom will not overflow the component.`
- `Use our candoitall components mcp server and example ... Chat page ...`

## Dependency And Sequencing Signals

- The main `/processes` page containment is the foundation for the modal containment work because both surfaces should follow the same height-propagation and pane-scroll rules.
- Browser proof must happen after both implementation subbundles because the key failure mode is visual.

## Validation Expectations

- Build the affected projects without introducing new compile failures.
- Extend targeted component and browser tests where the regression is observable.
- Run a large-screen browser proof on `/processes`, open the templates dialog, and verify internal pane scrolling plus Mermaid containment.

## UI Validation Strategy

- Run the first browser pass at a large desktop viewport.
- Review screenshots for readable text, no overlap, no clipping, and intentional use of space.
- If the desktop shell changes affect wrap or overflow, run one narrower follow-up viewport before closure.

## Browser Validation Analytics

- Subbundle 01 logs route `/processes`, desktop viewport, workspace-shell interactions, and a screenshot of the page-level containment result.
- Subbundle 02 logs route `/processes`, open templates dialog interactions, diagram zoom proof, and screenshots of the modal plus Mermaid preview state.

## Working Assumptions

- Standard page width remains acceptable; this bundle focuses on height containment and overflow control.
- Mermaid content may be clipped to its viewport during zoom as long as pan and zoom remain usable inside that viewport.

## Primary Risks

- Component tests cannot prove browser clipping behavior on their own.
- The templates modal currently uses a fullscreen dialog, so weak height propagation can create nested scrolling unless the body and inner shell are aligned correctly.
