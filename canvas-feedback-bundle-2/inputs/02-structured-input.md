# Structured Input

## Core Objective

Implement the four feedback items from `Feedback2.docx` inside the shared project structure canvas stack without regressing the existing workbench behavior.

## Hard Constraints

- Keep the change set inside the shared workbench and canvas components.
- Do not create page-specific CSS hacks when the shared canvas layer already owns the behavior.
- Keep markdown creation strongly typed through the catalog/create-request pipeline.
- Preserve existing PDF preview behavior other than its layering/placement.
- Keep the bundle and the implementation aligned as work completes.

## Source Artifacts

- `C:/Users/lucys/OneDrive - TechnicInsider/Produkty/CanDoItAll/feedbacks/Feedback2.docx`
- `C:\repositories\CanDoItAll\canvas-feedback-bundle-2\inputs\03-feedback2-extracted.md`
- `C:\repositories\CanDoItAll\canvas-feedback-bundle-2\inputs\feedback2-media\image1.png`
- `C:\repositories\CanDoItAll\canvas-feedback-bundle-2\inputs\feedback2-media\image2.png`
- `C:\repositories\CanDoItAll\canvas-feedback-bundle-2\inputs\feedback2-media\image3.png`

## Working Assumptions

- “Visible area of canvas” means the stage surface, not the whole browser page.
- Markdown upload can reuse the existing create-composer attachment path if the catalog definition exposes it.
- File-node visual tuning should reuse the existing palette system instead of introducing subtype-specific ad hoc classes.
- Preview dialogs should live inside the canvas overlay slot so maximized canvas mode cannot cover them.

## Primary Risks

- changing preview layering in the wrong place could break summary or Mermaid preview dialogs
- centering the help overlay too aggressively could hide the toolbar safe zone or reduce mobile usability
- making markdown require an attachment without preserving direct text entry would violate the request
- palette changes could over-darken nodes and make badges or text harder to read
