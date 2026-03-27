# Structured Input

## Core Objective

Implement the two feedback3 items inside the shared project structure workbench so runtime-capable nodes can launch the correct PowerShell session directly from the selection panel.

## Hard Constraints

- Keep the change set inside the shared workbench module and its typed metadata pipeline.
- Do not repurpose the existing node-command navigation flow for local shell launching.
- Do not add silent fallbacks when a node lacks the metadata needed to launch predictably.
- Keep the launch capability strongly typed around existing script and environment metadata.
- Preserve the existing inspector actions, preview flows, and attachment open behavior.

## Source Artifacts

- `C:/Users/lucys/OneDrive - TechnicInsider/Produkty/CanDoItAll/feedbacks/feedback3.docx`
- `C:\repositories\CanDoItAll\canvas-feedback-bundle-3\inputs\03-feedback3-extracted.md`
- `C:\repositories\CanDoItAll\canvas-feedback-bundle-3\inputs\feedback3-media\image1.png`

## Working Assumptions

- The requested buttons belong in the existing `Node actions` area of the selection panel.
- The feature is Windows-only because the request explicitly requires PowerShell and elevated PowerShell.
- Script nodes can launch from their stored command, arguments, and working directory.
- Environment nodes can launch from their typed metadata without introducing new catalog fields.

## Primary Risks

- deriving runtime commands incorrectly could launch the wrong project or ignore node settings
- mixing local shell launching into the routed artifact command pipeline would create a fragile ownership boundary
- elevated launch support can fail noisily on some Windows configurations, so the UI needs explicit feedback
- broadening eligibility too far could expose launch buttons on nodes that do not actually have enough metadata to run
