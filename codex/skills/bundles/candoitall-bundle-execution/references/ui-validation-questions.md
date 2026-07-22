# UI Validation Questions

Use these questions during UI validation. They are not optional for screenshot-driven or layout-sensitive work.

## Target Environment

- Start in a maximized headed browser window or the largest practical desktop viewport on the current machine.
- Capture a fullscreen or full-page screenshot from that large-screen pass.
- Make the visual judgement at the named desktop viewport.
- Record what useful part of the primary task is visible in the first viewport and identify the intended scroll owner.
- Do not spend time on narrower application widths unless the user explicitly requests them.
- For reusable basic `CanDoItAll.Components.BaseLib` components, also validate small, medium, and large viewports.
- For other shared libraries, preserve existing responsive behavior when touched without expanding responsive scope implicitly.

## Readability And Overlap

- Can I read all texts properly without zooming?
- Is any text clipped, faded into the background, or competing with nearby chrome?
- Is anything overlaying or colliding with something else?
- Are menus, tooltips, dropdowns, dialogs, floating windows, and inspectors layered correctly?
- When those overlays are open, is all of the intended content visible without container clipping or viewport clipping?
- When those overlays are open, do they stay clear of harmful left or right overflow that cuts off content?
- When those overlays are open, do they render above neighboring windows and chrome instead of hiding behind them?
- Was each relevant open state captured and inspected, rather than inferred from the closed-state screenshot?

## Layout Quality

- Is any component too large, too small, or visually disproportionate?
- Are there awkward gaps, unused zones, cramped clusters, or broken alignments?
- Are components aligned and justified consistently?
- Are we using the available space intentionally on the page?
- Are scroll containers obvious and usable, without hidden scrolling traps?

## Compact Task-First Review

- Is the primary working surface obvious and useful before page scrolling?
- Is supporting content placed without displacing the primary task, and is the stats treatment proportional to the role of those numbers?
- Is an independent create/edit flow in an appropriately sized dialog, or is the reason for an inline/split editor still valid?
- Are tabs used only for genuinely alternate supporting views, without hiding required actions or errors?
- Do text areas and dialogs fit realistic content without wasting height or forcing avoidable nested scrolling?
- Do item cards preserve clear media, identity/status, content/tags, and bottom action alignment, or would a list/table be clearer?
- Do affected compound controls adapt to their containing width, and were they inspected inside realistic narrow grid, card, rail, or dialog columns rather than only at full page width?
- Does whitespace still communicate grouping, with readable type and usable controls rather than a cramped result?

Use `candoitall-components-mcp/references/compact-ui-composition.md` for the decision rules behind these questions.

## System And Consistency

- Are shared components used where they should be, instead of ad hoc structures?
- Does the surface still feel like the existing app rather than a disconnected one-off patch?
- Do badges, icons, markers, and file-type cues remain visible on their backgrounds?
- Does the interaction model remain understandable for a new user?

## Frontend Skill Questions

When the screen is visually led, also ask:

- Is there one clear visual anchor or primary working surface?
- Is the hierarchy obvious in one glance?
- Would the layout still feel intentional if decorative shadows or effects were removed?
- Does motion, if present, improve comprehension rather than distract?

## Action Rule

If any answer is not acceptable, tune the layout, interaction, or composition and rerun the validation loop. Do not close the subbundle because `the test passed` while the screenshot still looks wrong.

When the current subbundle is a critical foundation for later work:

- record the answers in the execution report while the screenshot is in front of you
- record the first-viewport, scroll-owner, and open-overlay findings explicitly
- run one dependent-flow smoke or downstream surface check before allowing the next subbundle to begin
