# Selection Panel Density And Parity

This file defines what the moved panel must preserve and how its layout should be tightened.

## Non-Negotiable Behavior Parity

The in-canvas selection panel must preserve these current states:

- empty selection guidance
- multi-selection summary
- single-node summary
- attachment preview
- node actions
- grouped create-next-to-source actions
- connect mode guidance

## Required Multi-Select Expansion

Bundle 1 must add common-action handling for multi-selection.

At minimum, where the selected node set supports the action:

- status
- progress
- marker
- priority
- create border
- clear border

Display rule:

- show only actions valid for the full selected set or clearly label mixed-state behavior
- do not show single-node-only actions inside a multi-select panel unless they are explicitly scoped to one chosen item

## Single-Selection Content Model

Recommended section order:

1. compact identity header
2. high-value metadata
3. primary actions
4. attachment preview or file actions when relevant
5. create-next-to-source tools
6. lower-priority detail only when needed

Compact identity header should contain:

- node title
- node kind
- status
- important chips only
- optional route or context hint

High-value metadata should prefer dense rows over large tiles:

- artifact
- kind
- location
- owner or phase if useful
- progress
- priority
- marker

## Layout Density Rules

Use these rules when redesigning the panel:

- reduce vertical padding on routine sections
- reduce card-to-card gaps
- remove repeated explanatory text that does not change decisions
- convert obvious actions to icon buttons or short labels with tooltips
- avoid stacked cards when a simple divided section works
- keep only one dominant CTA per state
- keep create tools grouped and scannable without a long text wall
- minimize scrolling at common desktop widths

## Validation Questions For Density Review

These questions must be answered during implementation review:

- do we use the available space effectively?
- do gaps between controls consume space without improving clarity?
- can an action become an icon button with tooltip and still stay understandable?
- can the layout be rearranged to avoid scrolling at common desktop widths?
- do we keep unnecessary explanatory text that can be shortened?
- are margins and paddings large relative to the actual value of the content?
- is the primary action visible without scrolling?
- can the user understand the panel by scanning headings, chips, and the first action row only?
- do cards still need to be cards, or can some become plain sections?
- when multiple node states exist, is one state visually dominant and easy to parse?

## File And Media Node Expectations

File and media nodes must not use the same generic layout as abstract nodes.

They should prioritize:

- preview area when preview is meaningful
- open actions near the preview
- metadata compactly below
- local open action when supported

## Empty Selection State

Keep it compact.

Requirements:

- one short orientation sentence
- one compact cheat-sheet row or two
- no large dead area
- do not spend inspector area on copy that the toolbar or help surface already explains

## Single Source Of Truth

The panel layout can change, but behavior must stay driven by the same page logic and service calls already in the structure page.

This bundle is not a rewrite of structure actions. It is a controlled migration plus compaction.
