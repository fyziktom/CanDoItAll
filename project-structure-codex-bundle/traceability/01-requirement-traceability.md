# Requirement Traceability

| Requirement | Source artifact | Owning subbundles | Planned proof |
| --- | --- | --- | --- |
| `R01` Execute in order with gates | `04_PHASED_EXECUTION_PLAN.md`; `codex/TASK_SEQUENCE.md` | `P0-01` through `P3-02` | Subbundle gate table and dependency review |
| `R02` Preserve mapped behavior | `02_FEATURE_PRESERVATION_MAP.md` | `P0-01`, `P0-05`, `P0-06`, `P1-04`, `P2-02` | Targeted bUnit tests, Playwright scenarios, screenshots |
| `R03` Plain JS hot path | `03_TARGET_ARCHITECTURE_AND_OWNERSHIP.md`; `10_HTML_VS_JS_RENDERER_BOUNDARY.md` | `P0-01`, `P1-01`, `P1-02`, `P1-03`, `P2-01`, `P3-01` | Code inspection, build, focused browser regression |
| `R04` C# owns typed domain and persistence | `03_TARGET_ARCHITECTURE_AND_OWNERSHIP.md` | `P0-03`, `P0-04`, `P0-05`, `P1-04` | Component tests, persistence assertions, targeted service behavior |
| `R05` Real browser proof and screenshots | `07_VALIDATION_GATES_AND_SCREENSHOT_SCENARIOS.md` | All UI-visible subbundles | Playwright MCP analytics rows and artifact paths |
| `R08` Commit-only hot-path persistence | `05_PERFORMANCE_HOTSPOTS.md`; `06_PERFORMANCE_BUDGETS_AND_ACCEPTANCE.md` | `P0-02`, `P0-03`, `P0-04` | Counters, persistence-path assertions, drag and pan smoke |
| `R10` Retained and culled renderer | `00_EXECUTIVE_SUMMARY.md`; `05_PERFORMANCE_HOTSPOTS.md` | `P1-01`, `P1-02`, `P1-03`, `P2-01` | Debug counters, large-graph browser scenarios |
| `R12` Preserve PromptFactory | `02_FEATURE_PRESERVATION_MAP.md`; `11_DUPLICATION_AND_SHARED_SURFACE_RISK.md` | Shared-canvas subbundles | PromptFactory Playwright reruns |
| `R13` Preserve Sandbox | `02_FEATURE_PRESERVATION_MAP.md`; `11_DUPLICATION_AND_SHARED_SURFACE_RISK.md` | Shared-canvas subbundles | Sandbox smoke or documented blocker |
