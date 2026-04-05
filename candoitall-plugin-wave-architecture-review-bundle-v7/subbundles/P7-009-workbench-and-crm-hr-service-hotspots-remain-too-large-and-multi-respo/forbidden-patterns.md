# Forbidden patterns

The following patterns must not remain after the refactor for this item:

- any implementation shape that still reproduces the core problem described in `P7-009`
- ADR-only closure without code and tests
- hidden reintroduction through metadata, helper caches, or UI-only layers

## Local evidence to remove

- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs (3227 lines)
- src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs (5001 lines)
