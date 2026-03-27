
# Specification

## Item identity

- **Item ID:** I22
- **Title:** Prompt Factory eye-preview popover
- **Origin:** docx
- **Dependencies:** I21

## Objective

Make the component eye icon genuinely useful by showing a canvas-side floating preview popover with the component text.

## Normalized scope

Add hover or focus preview behavior for component rows so the eye icon opens a floating preview on the available side, preferring the right side.

### In scope

- Eye icon interaction behavior.
- Side-aware preview popover placement.
- Preview content rendering and overflow handling.

### Out of scope

- A full editor inside the preview popover.

## Key implementation decisions

- Popover placement should prefer the right side but fall back intelligently when space is constrained.
- The preview must remain inside the visible canvas and not obscure the entire toolbox.
- Preview content should come from the actual component text or summary, not placeholder text.

## Implementation tasks

- Add a preview trigger on the eye icon.
- Implement available-side placement logic with right-side preference.
- Render component text or summary inside a floating preview window.

## Risks to control

- Preview windows that overflow the canvas will feel broken immediately.

## Covered original notes

- N146 — Mouseover on icon of eye on component line show inside canvas popup floating window on available side (if right available, then prefer it) with text of component.
