
# I18 — Arrow links, side-aware placement, and mindmap image export

## Objective

Fix spatial logic and give users clearer connection semantics and exportable visuals.

## Why this item exists

Add directional arrow support, export the mindmap as an image, and fix child placement so new nodes are created on the side implied by the connection geometry.

## Covered original notes

- N133 — Connection between nodes with additional arrow
- N134 — Export mindmap as image
- N135 — Node should be placed on the side where it should connect. For example if I move some node to left side of the canvas, it connects to parent node from right side, then I add new node that is connected under that node, it connects it from left side, but place it on right side of the node. It must place it to side where it is connected.

## Dependencies

- I17 — Relationship editing, delete behavior, and borders

## Files in this folder

- `README.md` — quick overview
- `SPECIFICATION.md` — normalized implementation scope
- `FILE_REFERENCES.md` — current code hotspots and likely new files
- `IMPLEMENTATION_PROMPT.md` — Codex implementation prompt for this item
- `VALIDATION_PROMPT.md` — QA and validation prompt for this item
- `ACCEPTANCE_CRITERIA.md` — pass or fail outcomes
- `CHECKLIST.md` — task checklist
- `SCREENSHOT_REQUIREMENTS.md` — screenshot evidence required for this item

## Delivery rule

This item is not complete until its acceptance criteria, test requirements, and screenshot requirements are all satisfied.
