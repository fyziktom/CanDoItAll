# Structured Inputs

## User Input Rewritten

### 1. Help popovers must dismiss gracefully
- Current `?` help bubbles look good.
- They must close when the user clicks outside.
- They should also close on mouse leave with a short delay so they do not feel sticky.
- The interaction must feel predictable and lightweight.

### 2. The page still creates too much scroll debt
- The current page keeps too much content under the canvas.
- Users should not need to scroll through support content just to keep working in the canvas.
- The canvas and inspector should be the primary workspace.
- The lower content should move behind real tabs so the user opens only the lane they need.
- `Canvas` must be the first tab.
- When a non-canvas tab is active, the canvas and right inspector must disappear.

### 3. New prompt sessions need a guided setup flow
- A new prompt mindmap should offer a basic setup wizard directly in the canvas experience.
- That setup should remain accessible later, including in maximized canvas mode.
- The setup should prefill known values from project structure when available.
- If only some values are known, the wizard should ask for the rest.
- The setup should remain editable later.

### 4. The setup flow should capture prompt intent and environment
- What kind of prompt is this: programming, business, marketing, research, operations, other.
- If programming, what is the main language.
- Optional secondary languages.
- Is this for an existing app or a new app.
- What is the working repository.
- What are the reference or source repositories.
- Other context that helps the AI understand the working frame.

### 5. Prompt-component picking needs a different interaction model
- The hexagonal radial system is good for generic, compact actions.
- It is not ideal for browsing many prompt components.
- Prompt components need a list-oriented popup with search and grouped sections.
- The mental model should be closer to a toolbox or accordion explorer.
- Hovering a component should show a compact preview of its prompt text or summary so the user can choose confidently.

### 6. Prompt flow should support rich attachments
- Users must be able to attach any file type.
- Inputs should support image, video, spreadsheet, PDF, text, and other files.
- The UI should encourage the user to state what the AI should extract or use from that input.
- Canvas nodes should look visually distinct by input type, for example PDF red and spreadsheet green.

### 7. The system needs more guidance and more guardrails
- Prompt Factory is powerful but easy to get lost in.
- It needs more guidance without becoming restrictive.
- It should prevent accidental large changes.
- If an action may add or replace many items, the system should warn the user and ask for confirmation.
- Users should always have a clear recovery path.

### 8. The right inspector must become contextual
- The current right panel still duplicates too much of the page-level work.
- It should primarily show the currently selected canvas item.
- When the user clicks a setup node, component, attachment, branch, or prompt step, the inspector should show that item's specific details and actions.
- Page-wide work such as setup forms, governance exploration, assembly packing, and review should live in their own tabs instead.

## Refined Product Intent

Prompt Factory should behave like a guided expert workbench:
- flexible enough for advanced prompting
- structured enough for ordinary users to stay oriented
- safe enough to avoid destructive surprises
- compact enough to keep attention on the active decision

## Non-Negotiable UX Principles
- One dominant workspace at a time.
- The page-level tab is the workspace switch. Canvas is one workspace, each later stage is another.
- Every heavy action needs preview or confirmation.
- The canvas must keep setup reachable, not hidden behind a long page.
- Search and hover preview are mandatory for large prompt-component libraries.
- Inputs must communicate both source and intended extraction.
