# Verification plan — PRM-F09

## Expected verification outcomes

- Users can model canonical graph semantics without layout becoming truth.
- Layout survives re-open.
- Role/template pickers and staffing-status cues behave correctly.
- Wave 1 designer works without waiting for Wave 2 handoff-label chrome.

## Automated tests

- Component tests for designer rendering, edit actions, and panels
- Integration tests for layout persistence and semantic save flows
- Playwright tests for create/edit/save/reopen process-modeling paths

## Manual verification checklist

1. Create a draft process and place nodes.
2. Connect nodes and save layout.
3. Edit role/template metadata from the designer.
4. Re-open the draft and confirm semantics/layout remain consistent.
5. Confirm no semantic dependency on later handoff-label visuals.

## Regression concerns to watch

- Layout treated as canonical truth
- Overly broad CanvasLib changes
- Role-template picker coupling the designer directly to future AgentFramework runtime concerns
