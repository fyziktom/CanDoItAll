# Canvas Improvements Implementation Tracker

This tracker turns the handoff docs into an execution checklist for the implementation pass.

## 1. Shared canvas foundation

- [x] Add shared canvas DTO/contracts in `CanDoItAll.ComponentKit`.
- [x] Add shared canvas host component in `CanDoItAll.ComponentKit`.
- [x] Add shared canvas JS runtime in `CanDoItAll.ComponentKit/wwwroot/js`.
- [x] Add shared canvas CSS/tokens in `CanDoItAll.ComponentKit/wwwroot`.
- [x] Wire shared static assets into the Blazor host.
- [x] Support node rendering, links, zoom, pan, fit, selection, multi-selection, marquee selection, and node dragging.
- [x] Support context actions, quick-create rail, help overlay, maximize mode, and keyboard shortcuts.
- [x] Support persisted UI state with versioned JSON payloads.

## 2. Shared stage layout and chrome

- [x] Build the shared stage shell: header, canvas stage, right inspector, lower supporting panels.
- [x] Match the warm-to-cool background, rounded host, inspector card, and purple zoom rail.
- [x] Add desktop hint pill, help surface, and top-left/top-right chrome actions.
- [x] Keep the stage responsive for desktop, tablet, and mobile widths.

## 3. Project Structure migration

- [x] Replace the current `ProjectStructureCanvas` internals with the shared canvas host.
- [x] Map project structure nodes/links into the shared canvas contract.
- [x] Preserve current commands: open, branch, validate, test, skip, mark used, and link.
- [x] Add adjacent create placement rules so new nodes do not overlap the source node.
- [x] Add inspector parity for empty, single-select, and multi-select states.
- [x] Keep outline and supporting surfaces available below the main stage.
- [x] Persist shared canvas state without breaking existing workbench data.

## 4. Prompt Factory conversion

- [x] Convert `PromptFactoryPage` into a canvas-first editor while keeping the wizard steps.
- [x] Map prompt session, branch groups, and prompt steps into shared canvas nodes.
- [x] Move primary editing into the right inspector.
- [x] Keep governance, preview, and session actions in lower supporting panels.
- [x] Persist prompt canvas UI state with the prompt session.
- [x] Preserve branch creation and linked prompt opening behavior.

## 5. Testing and QA

- [x] Add/adjust integration coverage for new shared state and prompt session persistence.
- [x] Add/adjust component coverage for the new page structure.
- [x] Add/adjust Playwright coverage for selection, zoom, pan, and primary canvas actions.
- [x] Run build and relevant test projects successfully.
- [x] Verify both editors manually with Playwright MCP.
- [x] Capture desktop-width screenshots for both editors.
- [x] Capture at least one compact-width screenshot to check responsive behavior.

## 6. Final signoff

- [x] Both editors visibly share one canvas system.
- [x] Both editors use the same visual system and chrome.
- [x] Project Structure keeps its domain behavior after the migration.
- [x] Prompt Factory is no longer a list-first flow editor.
- [x] Manual browser QA shows no major parity gaps against the plan.
