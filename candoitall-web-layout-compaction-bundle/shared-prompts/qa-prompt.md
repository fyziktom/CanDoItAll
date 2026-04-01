# QA Prompt

Validate the active subbundle of `candoitall-web-layout-compaction-bundle` with real browser proof.

## Required Checks

- Start with a large desktop viewport around `1720x1160`.
- Re-check narrower widths after the desktop layout is stable.
- Review the screenshot, do not only capture it.
- For every affected route or dialog, answer:
  - Can I read all important text without zooming?
  - Is anything overlapping, clipped, or hidden?
  - Does the first screen reach action faster than before?
  - Is the available width used intentionally?
  - Are search, filters, and reset aligned consistently where required?
  - If helper copy moved behind `?`, does the affordance still expose the information cleanly?
  - If a modal or overlay is open, is all content visible and above neighboring chrome?

## Required Evidence

- Route
- Viewport
- Browser commands or actions
- Screenshot path
- DOM or text assertions when relevant
- Pass/fail decision
- Reopen note if the proof is weak

