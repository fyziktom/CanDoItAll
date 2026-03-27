
# Specification

## Item identity

- **Item ID:** I08
- **Title:** Typed file nodes and Mermaid viewer
- **Origin:** docx
- **Dependencies:** I01

## Objective

Give file nodes clear meaning on the canvas and add diagram-aware viewing for Mermaid content.

## Normalized scope

Add typed file node visuals for pdf, excel, docx, txt, json, md, and Mermaid with color coding, icons, and diagram detection metadata.

### In scope

- Typed file node variants and visual mapping.
- Mermaid viewer and diagram-type detection feedback.
- Subtype labels and preview affordances.

### Out of scope

- A full-blown document editor for every file format.

## Key implementation decisions

- Represent files through one file-family model with deterministic subtype-to-color and subtype-to-icon mapping.
- Treat Mermaid as a special file-like subtype with viewer support and automatic diagram type detection.
- Keep file color semantics consistent across canvas cards, inspectors, and previews.

## Implementation tasks

- Create subtype mappings for the requested file types.
- Apply stable color and icon tokens for each subtype.
- Add Mermaid detection and viewer affordances.
- Expose detected diagram type on the node or in details.

## Risks to control

- Users cannot trust the canvas if file colors and icons are inconsistent or ambiguous.

## Covered original notes

- N064 — Files
- N065 — Own menu item for pdf, excel, docx, txt, json, md
- N066 — Each of those files nodes must have proper color (pdf red, excel green, docx blue, txt, json and similar probably gray with icon/text of type, etc)
- N067 — Mermaid (plus viewer)
- N068 — Auto identification of graph type and info about it on node.
