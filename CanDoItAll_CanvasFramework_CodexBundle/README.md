# CanDoItAll Canvas Framework Codex Bundle

This bundle was generated from analysis of the uploaded `CanDoItAll-main.zip` repository and is intended to give Codex implementation agents enough concrete context to build a long-lived shared canvas framework for CanDoItAll.

The bundle is intentionally written in English because the target audience is implementation and validation agents. The user-facing delivery summary can remain in Czech.

## Included outcomes

- Full current-state analysis of the relevant canvas-based surfaces.
- Target architecture for a reusable canvas framework with a graph-workbench family and a calendar family.
- Complete component inventory with status, priority, dependencies, and implementation scope.
- Detailed per-component realization packages for **62** components.
- Integration bundle with file-specific migration guidance and anti-duplication rules.
- QA, UX/UI, architecture review, and future-feature simulation.

## Inventory summary

- Components: **62**
- Shared components: **50**
- Domain-specific components: **12**
- Existing: **2**
- Partial: **41**
- Missing: **19**
- Priority P0: **27**
- Priority P1: **26**
- Priority P2: **9**

## Bundle structure

```text
CanDoItAll_CanvasFramework_CodexBundle/
├── README.md
├── 00_BUNDLE_STRUCTURE.md
├── 01_ANALYSIS_CURRENT_STATE.md
├── 02_TARGET_ARCHITECTURE.md
├── 03_COMPONENT_INVENTORY.md
├── 04_IMPLEMENTATION_ROADMAP.md
├── 05_FILE_REFERENCE_CATALOG.md
├── 06_QA_UX_ARCHITECTURE_REVIEW.md
├── 07_FUTURE_FEATURE_SIMULATION.md
├── 08_EXECUTIVE_SUMMARY.md
├── 09_KONVA_REFERENCE_EXTRACTION.md
├── bundle_manifest.json
├── component_inventory.json
├── component_inventory.csv
├── file_reference_catalog.json
├── components/
│   ├── _INDEX.md
│   └── <62 component folders>/
└── integration/
    ├── README.md
    ├── INTEGRATION_STRATEGY.md
    ├── IMPLEMENTATION_ORDER.md
    ├── DEPENDENCY_MAP.md
    ├── dependency_map.json
    ├── REFACTORING_PLAN.md
    ├── MIGRATION_PLAN.md
    ├── SHARED_COMPONENT_MAP.md
    ├── DUPLICATION_REPLACEMENT_MAP.md
    ├── RISKS.md
    ├── ANTIPATTERNS.md
    ├── IMPLEMENTATION_PROMPTS.md
    ├── VALIDATION_PROMPTS.md
    └── CHECKLISTS.md
```

## How to use this bundle

1. Read `00_BUNDLE_STRUCTURE.md` and `08_EXECUTIVE_SUMMARY.md`.
2. Read `01_ANALYSIS_CURRENT_STATE.md` and `02_TARGET_ARCHITECTURE.md`.
3. Use `03_COMPONENT_INVENTORY.md` plus `components/_INDEX.md` to choose implementation targets.
4. For each chosen component, use the folder-specific `IMPLEMENTATION_PROMPT.md` and `VALIDATION_PROMPT.md`.
5. Use the `integration/` folder before editing pages or shared JS files.
6. Re-run the QA and future-feature simulation checklists after each implementation wave.

## Strategic conclusion in one paragraph

The repository already contains a **strong shared graph-workbench shell** (`CanvasWorkbench`) and a **promising shared calendar wrapper** (`CanvasCalendar`), but the graph side is still missing a clean low-level scene foundation and the calendar side is still blocked by legacy page integration. The correct strategy is **not** to build a second parallel canvas system. The correct strategy is to **decompose and harden the existing shared components**, introduce missing low-level primitives and interaction subsystems, extract domain adapters out of page files, retire the legacy workbench wrappers, and keep the calendar runtime as a sibling specialized engine under the same host, state, diagnostics, and integration conventions.
