# Normalized Requirements

| Id | Requirement | Validation |
|---|---|---|
| R1 | Project structure canvas exposes an Agents floating-window toggle. | Playwright opens `/projects/{id}/structure`, clicks the toggle, and captures the open launcher. |
| R2 | Process definition canvas exposes an Agents floating-window toggle. | Playwright opens `/processes` or `/projects/{id}/processes`, switches to Steps, clicks the toggle, and captures the open launcher. |
| R3 | The launcher shows only agents with matching access metadata for the current surface and scope. | Component proof plus browser-visible access badges. |
| R4 | Access badges show Read, Write, or both. | Browser screenshot shows badges on listed agents. |
| R5 | Launcher has a text search line. | Playwright enters search text and verifies filtered results. |
| R6 | Launcher has tag search using `TagEditor`. | Playwright adds a tag filter and verifies the filtered list. |
| R7 | Double-clicking an agent opens a second floating chat window. | Playwright double-clicks a launcher item and captures the chat window. |
| R8 | The chat window creates a new persisted thread for the selected agent. | Agents page chat tab lists the contextual thread after the prompt is sent. |
| R9 | The chat window reuses existing chat functions. | It uses `ChatWorkspacePanel` for composer, approvals, attachments, title edit, runtime details, and execution stream. |
| R10 | Project validation sends a calculator-roadmap prompt. | Browser proof includes project contextual chat after prompt send. |
| R11 | Process validation sends a review-role prompt. | Browser proof includes process contextual chat after prompt send. |
