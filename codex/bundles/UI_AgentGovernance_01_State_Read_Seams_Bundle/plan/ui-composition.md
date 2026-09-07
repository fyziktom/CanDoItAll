# Preserve the actual Governance composition

Primary surface: real `ListDetailShell`, 24rem run list, detail pane and existing page header. Supporting controls are `ListPanelHeader`, compact Refresh action and `InputSelect<Guid?>` agent filter with explicit All technical agents. Run rows remain real SelectionListItem with state/outcome badges and date/summary. Preserve first-run default, order, summaries and visual density unless a specific witnessed correction is needed.

Detail keeps header/title, CompactStatStrip (approvals/artifacts/checkpoints/receipts), run/source/process summary, two-column approval/artifact/checkpoint/receipt cards, and the real timeline/metrics children. Do not replace these with text-only fixtures, generic div cards or a new design. There is no editor textarea or mutation/approval form to redesign.

Freeze production-standard **1600 x 1000** desktop viewport, actual page frame and browser zoom. Review 1280px and 900px only as targeted long-content/responsive checks, not a new universal mobile redesign. Preserve the existing shell's list/detail scroll ownership and first-viewport density; record computed overflow, min-width, list width and detail geometry before edits. Avoid nested independent scroll regions invented solely to fit screenshots.

Add compact lane-specific loading/error/stale/retry inside the relevant list/detail area. A new agent request hides prior-target data. Same-target accepted data can remain visibly stale. Missing agent/run has a clear target-aware unavailable view. Run detail failure preserves valid list/filter/context.

Browser review states: initial loading; empty; ready with every real section; missing agent; stale list; detail failure with list preserved; A/B and R1/R2 replacement; pending disposal; long labels/summaries/artifacts/approvals; failed/waiting/completed badges. Exercise actual agent dropdown and compact-stat tooltips open; inspect overlay clipping/stacking and screenshots. No new Governance dialog is planned. Keep unrelated overlays unchanged if navigating a public page test.

Current Governance has no isolated CSS file. G02 may add scoped CSS only for a real composition need supported by computed layout evidence. Shared responsive compatibility remains the single existing Tailwind/main/component-layout-utilities.css imported by Web/Fast. G03 must discover three legitimate CSS probes from the actual consumed asset pipeline; a missing measurable CSS owner is a baseline blocker/replan, not justification for synthetic benchmark styling.
