# UI Validation Questions

Use these questions during UI validation. They are not optional for screenshot-driven or layout-sensitive work.

## First Pass Environment

- Start in a maximized headed browser window or the largest practical desktop viewport on the current machine.
- Capture a fullscreen or full-page screenshot from that large-screen pass.
- Do the first visual judgement there before shrinking to narrower widths.
- After the large-screen pass is acceptable, continue with narrower desktop, tablet, or mobile widths when the change affects layout or responsiveness.

## Readability And Overlap

- Can I read all texts properly without zooming?
- Is any text clipped, faded into the background, or competing with nearby chrome?
- Is anything overlaying or colliding with something else?
- Are menus, tooltips, dropdowns, dialogs, floating windows, and inspectors layered correctly?
- When those overlays are open, is all of the intended content visible without container clipping or viewport clipping?
- When those overlays are open, do they stay clear of harmful left or right overflow that cuts off content?
- When those overlays are open, do they render above neighboring windows and chrome instead of hiding behind them?

## Layout Quality

- Is any component too large, too small, or visually disproportionate?
- Are there awkward gaps, unused zones, cramped clusters, or broken alignments?
- Are components aligned and justified consistently?
- Are we using the available space intentionally on the page?
- Are scroll containers obvious and usable, without hidden scrolling traps?

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
- run one dependent-flow smoke or downstream surface check before allowing the next subbundle to begin
