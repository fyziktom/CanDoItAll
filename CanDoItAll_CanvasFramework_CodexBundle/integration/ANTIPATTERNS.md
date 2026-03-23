# Anti-patterns

- Do not create a second parallel shared graph wrapper next to CanvasWorkbench. Extend and decompose the existing one.
- Do not keep page-level graph projection logic in ProjectStructurePage or PromptFactoryPage once dedicated adapters exist.
- Do not duplicate lifecycle wrappers for calendar and graph surfaces; route both through CanvasSceneHost conventions.
- Do not move business workflows into JS when they can remain typed and testable in C#.
- Do not force the calendar runtime to pretend it is the same scene graph as the graph workbench. Share host contracts and utilities, not inappropriate internals.
- Do not add one-off node-card HTML fragments directly inside page files; use NodeCardComposer templates.
- Do not let future shortcut, floating-inspector, or recommendation helpers live in generic runtime files without explicit module boundaries.
- Do not parse persisted state with page-local JSON probing when typed persistence models can own schema evolution.
