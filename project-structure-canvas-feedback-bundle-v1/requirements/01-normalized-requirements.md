# Normalized Requirements

## RQ-01 Semantic Node Visual Presets

- Owns `N001`.
- Every canvas node category used by the project-structure workbench must resolve to a distinct semantic visual preset.
- The preset contract must live on the node visual property path, not as scattered component-only or adapter-only styling.
- Presets must use the existing Tailwind style system for color and effect tokens.
- At minimum, the shipped presets must satisfy the explicit examples in the raw request: PDF red, Excel green, deployment blue, and related infrastructure colors that remain semantically coherent.

## RQ-02 Multiline Inline Notes

- Owns `N002`.
- Inline simple-note editing must allow `Shift+Enter` to insert a newline.
- The saved note must preserve multiline content in the persisted node and in the rendered canvas.
- The save and cancel behavior must remain explicit and predictable.

## RQ-03 Node Id Copy Actions

- Owns `N003`.
- The selected node surface must expose one action that copies only the selected node id.
- The same surface must expose a second action that copies the full descendant id structure beneath the node.
- The descendant copy output must be deterministic and preserve hierarchy order.

## RQ-04 Subtree Cut And Paste

- Owns `N004`.
- `Ctrl+X` and `Ctrl+V` must operate on the selected node and include all descendants under that node.
- Paste must preserve the subtree structure and relative layout semantics while creating valid destination nodes and ids.
- The cut flow must update both persisted project structure state and the rendered canvas without leaving orphaned descendants.

## RQ-05 Move Descendants Into Subproject

- Owns `N005`.
- The workbench must expose a supported flow that moves all descendants of a selected node into a subproject target.
- The flow must preserve descendant hierarchy and produce a valid subproject relationship in the projects module.
- The source canvas must refresh to reflect the move and the destination subproject must receive the transferred structure coherently.

## RQ-06 Change Block Type For Common Blocks

- Owns `N006`.
- The workbench must support changing the block type for common catalog-backed blocks.
- Type mutation must preserve compatible shared metadata instead of forcing delete and recreate.
- Changed blocks must re-resolve their semantic visual preset from the same central preset architecture used for creation.

## RQ-07 Common Computer Block

- Owns `N007`.
- The standard block catalog must expose a new common computer block.
- The block must participate in the same create, selection, and mutation flows as existing common blocks.
- The block must ship with a coherent preset, iconography, and searchable catalog presence.

## RQ-08 Convert Simple Note To Block

- Owns `N008`.
- A simple note must be convertible into a supported block type.
- The conversion flow must derive the destination block title from note text and preserve the note body as block content or notes.
- The conversion must use the same mutation and preset architecture as other common block changes.

## RQ-09 Router And WiFi Common Blocks

- Owns `N009`.
- The standard block catalog must expose a router block and related WiFi subblocks or variants called out by the request.
- These presets must be searchable, visually distinct, and compatible with block-type change flows.

## RQ-10 Mandatory Validation And Closure

- Owns the raw mandatory directions.
- Every shipped subbundle that touches browser-visible behavior must be validated through Playwright MCP and screenshots.
- Bundle closure is blocked until execution analytics, screenshot evidence, and raw note closure rows are populated in the execution report.
