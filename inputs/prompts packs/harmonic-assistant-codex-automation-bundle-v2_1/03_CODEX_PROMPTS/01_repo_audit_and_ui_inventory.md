# 01 — Repo audit & UI inventory

Goal: capture current state and ensure we do not break working features.

Tasks:
1) Identify all remaining Blazor controls on `Harmony.razor` related to:
   - mood, module toggles, midi connect, recording, import/export, history length, debug.
2) Create a checklist in the PR description (or in docs) listing:
   - control name
   - current binding/service method
   - target canvas widget
3) Ensure JS renderer currently supports:
   - text scaling (A-/A+)
   - mood brightness/colorfulness controls
   - node radius scaling with fontScale

Acceptance:
- You produce a short markdown note `docs/harmonic-assistant/ui-inventory.md` with mapping table.
- App builds.

Self-check:
- `dotnet build`
