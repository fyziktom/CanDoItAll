# Risks

## Technical risks

| Risk | Why it matters | Mitigation |
| --- | --- | --- |
| Monolithic JS refactor risk | Splitting the workbench JS by concern can accidentally break runtime state if done in one giant edit. | Refactor behind stable public exports and add smoke tests after each extraction. |
| Page migration drift | Adapters may be added but pages may continue owning hidden mapping logic. | Use explicit deletion checklist items for page-owned mapping methods. |
| Calendar migration regression | ProjectCalendarPage may regress behavior when moving from legacy wrapper to CanvasCalendar. | Preserve wrapper parity first, then modularize internals. |
| Selection/state schema drift | Old persisted JSON may not match new typed state models. | Introduce versioned persistence envelopes and migration-aware parser helpers. |

## UX/UI risks

| Risk | Why it matters | Mitigation |
| --- | --- | --- |
| Selection clarity regression | Refactoring overlays could reduce clarity of current selection or group-frame visuals. | Add visual regression checks and explicit selection-state snapshots. |
| Hover/focus conflicts | New tooltip/popover/floating inspector systems can create focus traps or stale hover states. | Introduce HoverFocusRouter early and validate focus return. |
| Create menu discoverability | Moving to shared create/action components could flatten domain nuance. | Keep domain adapters responsible for grouping and labels. |

## Performance risks

| Risk | Why it matters | Mitigation |
| --- | --- | --- |
| Connector recalculation cost | More advanced overlays and anchors can increase geometry churn. | Use InvalidationScheduler and path caching. |
| DOM-heavy node rendering at scale | Rich node cards may not scale to very large scenes without virtualization. | Introduce viewport-aware realization and minimap support. |
| Calendar widget over-refresh | Wrapper changes could accidentally trigger full widget rebuilds for small updates. | Use typed update APIs and wrapper-level diffing. |
