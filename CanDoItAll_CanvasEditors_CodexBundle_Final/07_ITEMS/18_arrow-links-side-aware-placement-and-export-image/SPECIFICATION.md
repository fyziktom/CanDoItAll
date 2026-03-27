
# Specification

## Item identity

- **Item ID:** I18
- **Title:** Arrow links, side-aware placement, and mindmap image export
- **Origin:** docx
- **Dependencies:** I17

## Objective

Fix spatial logic and give users clearer connection semantics and exportable visuals.

## Normalized scope

Add directional arrow support, export the mindmap as an image, and fix child placement so new nodes are created on the side implied by the connection geometry.

### In scope

- Connection arrow rendering or settings.
- Side-aware placement policy.
- Mindmap image export flow.

### Out of scope

- A full vector export suite beyond the requested image export.

## Key implementation decisions

- Placement must respect the side of the parent-child connection instead of always biasing right.
- Directional arrows should be additive and not break existing connector visuals.
- Image export should produce a faithful canvas representation suitable for sharing.

## Implementation tasks

- Fix placement policy to use connection side rather than a hard-coded right bias.
- Add arrow rendering or arrow-enabled connection variants.
- Implement image export and ensure the canvas output matches the visible scene.

## Risks to control

- Side-aware placement can become unstable if connector geometry is not persisted consistently.

## Covered original notes

- N133 — Connection between nodes with additional arrow
- N134 — Export mindmap as image
- N135 — Node should be placed on the side where it should connect. For example if I move some node to left side of the canvas, it connects to parent node from right side, then I add new node that is connected under that node, it connects it from left side, but place it on right side of the node. It must place it to side where it is connected.
