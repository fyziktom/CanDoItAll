# QA And UX Review

## Critical Review Of The Inputs

The original inputs were directionally strong but needed tighter product framing in a few areas.

### Improvement 1: wizard should not feel blocking
- A mandatory full-screen wizard would slow expert users down.
- Better solution: a persistent setup node with a guided editor that is visible early and reachable later.

### Improvement 2: tabs must be real workspaces, not just content switches
- A fake tab strip that only swaps lower content still leaves the page visually overloaded.
- Better solution: use true page tabs where `Canvas` is one workspace and `Setup`, `Governance`, `Assembly`, and `Review` are separate workspaces.

### Improvement 3: the inspector must be contextual, not a duplicate page
- If the right panel mirrors whole stage workspaces, the scroll problem just moves sideways.
- Better solution: keep the inspector for selected-node detail and actions, and move broad workspace editing into the matching page tabs.

### Improvement 3A: the inspector must stay with the canvas
- If the inspector lives outside the canvas, maximized graph work still feels split across two surfaces.
- Better solution: move the inspector into the canvas as a floating tool panel that can be dragged or minimized.

### Improvement 3B: contextual editing must feel live and local
- If a selected prompt component opens only metadata, the inspector still feels passive.
- Better solution: show the effective session prompt text directly in the inspector and let the user edit it there.

### Improvement 3C: selected-item preview needs a fast copy path
- Users should not need a full prompt build every time they want to inspect one branch or one component.
- Better solution: expose a selection preview modal that can assemble the selected subtree or item into a copy-ready text slice.

### Improvement 3D: the inspector must not waste the first screen on filler
- Generic explanatory cards above the real selected-item content create scroll debt and bury the primary action.
- Better solution: start component and group states with the real selected-item summary, then show editor or selected-items actions immediately.

### Improvement 3E: overlays must beat the canvas chrome
- If the large editor modal sits below the canvas shell or maximized chrome, the UI feels broken even when the action technically fired.
- Better solution: keep editor modals on a higher layer than the canvas workbench and floating inspector.

### Improvement 4: radial menu should not be removed globally
- The radial system is efficient for compact generic actions.
- Better solution: keep radial for generic actions and switch only component browsing to a toolbox-style panel.

### Improvement 5: any file type needs semantic intent, not only upload
- Uploading files without telling the AI what to do with them creates weak prompts.
- Better solution: every rich input should capture extraction or usage intent.

### Improvement 6: safety should focus on high-impact actions
- Confirming every action would create friction.
- Better solution: confirm only bulk replace, clear, and reset actions with clear impact counts.

## Acceptance Decision

Accepted with the following refinements:
- canvas-first default
- real page tabs instead of a fake support-lane button strip
- contextual floating inspector instead of duplicate right-side workspaces
- setup node instead of blocking wizard
- toolbox panel only for prompt components
- semantic file attachments
- targeted confirmations for bulk actions

## QA Approval Gate

The implementation is acceptable only if all of the following are true:
- the active page tab is the only major workspace rendered
- the active tab is visually connected to the active panel and reads as a standard tab pattern
- non-canvas tabs do not show the canvas or floating inspector
- the external inspector column is gone on the canvas tab
- the floating inspector is available inside normal and maximized canvas modes
- the floating inspector can be minimized and restored without losing selection context
- the floating inspector can be dragged without getting stuck or leaving the visible canvas
- a new user can understand what to do within the first screen
- an advanced user can continue working without modal friction
- selected component editing is obvious and clearly session-scoped
- previewing a selected item opens the right slice, not the entire prompt by accident
- expanding component editing always opens a usable modal above the canvas
- component and group inspector states lead with useful content, not filler copy
- the component picker is more precise than the current radial browsing
- attachment nodes communicate file type and intended use
- destructive or heavy actions are no longer one-click surprises
- screenshots show a compact, calm, high-clarity interface with no obvious spacing or hierarchy problems

## Review Status

Bundle status: approved as the implementation contract.
