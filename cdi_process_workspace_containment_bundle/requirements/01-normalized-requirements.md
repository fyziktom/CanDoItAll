# Normalized Requirements

## R001 Process Page Height Contract

- The `/processes` workspace must fit inside the available page viewport instead of growing the entire document when the definition list or detail content is long.

## R002 Definition List Internal Scroll

- The process-definition card list must scroll inside the list pane while the header and the detail pane remain stable.

## R003 Detail Tab Internal Scroll

- The process detail tabs must fill the available detail-pane height and keep tab content scrollable inside the selected panel.

## R004 Templates Modal Internal Scroll

- The fullscreen templates dialog must keep both the template list pane and the preview pane internally scrollable without relying on an extra outer body scroll for their normal operation.

## R005 Mermaid Preview Containment

- Mermaid diagrams rendered in the templates dialog must stay visually bounded inside their preview surface during zoom and pan interactions.

## R006 Shared Component Compliance

- The containment fix must use existing BaseLib page, list-detail, and tabs primitives before introducing local structural CSS.

## R007 Browser Proof

- Closure requires browser-backed proof on `/processes`, including an open templates dialog, list/detail scrolling, and Mermaid interaction evidence.
