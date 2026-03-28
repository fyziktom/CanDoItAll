# Codex master prompt

You are working in the `CanDoItAll` repository on the `ProjectStructurePage` workbench.

## Mission

Refactor the current ProjectStructure workbench so it becomes significantly more performant and maintainable **without breaking any mapped functionality**.

The intended direction is:

- more responsibility in **plain JavaScript** for the hot path,
- typed domain logic and persistence in **C#**,
- HTML/Blazor retained for rich overlays,
- no TypeScript,
- no reckless big-bang rewrite.

## Ground truths from the audit

1. The current runtime scene is DOM + SVG, not a true live HTML5 canvas renderer.
2. The largest bottlenecks are currently:
   - full layer rebuilds,
   - overlay event leakage,
   - InteractiveServer chatter during active interaction,
   - excessive DB persistence in hot paths,
   - full reloads after simple mutations.
3. Shared canvas code is also used by PromptFactory and Sandbox, so shared changes require cross-regression.
4. The user explicitly wants a more JS-owned hot path, but still wants models/domain logic to stay in C# where it makes sense.
5. The package for this work must stay fully English, and all source-code comments must remain in English.

## Non-negotiable rules

1. **Preserve all mapped features.**  
   Read `02_FEATURE_PRESERVATION_MAP.md` first.

2. **Do not start with a full true-canvas rewrite.**  
   Follow the task order from `codex/TASK_SEQUENCE.md`.

3. **Use plain JavaScript only.**  
   Do not introduce TypeScript.

4. **Keep the public workbench API stable unless the task explicitly requires migration.**

5. **Add or update tests whenever behavior or risky shared code changes.**

6. **Use screenshot/browser validation for visible workbench changes.**

7. **If a validation gate fails, fix it and rerun until it passes.**  
   Do not move to the next task with known failures.

## Ownership intent

### JS should own
- scene rendering and patching,
- viewport culling,
- drag/pan/zoom loop,
- hit testing,
- overlay-vs-scene event routing,
- transient floating-window geometry,
- debug counters and render instrumentation.

### C# should own
- typed domain models,
- service methods and transactions,
- create/edit/delete/link/hierarchy commands,
- action catalogs,
- graph adapters,
- committed persisted state.

### HTML/Blazor should continue to own
- toolbox,
- selection/health windows,
- dialogs,
- previews,
- uploads,
- summary modal,
- transcript confirmation,
- mermaid viewer.

## Required workflow for every task

1. Read the task brief.
2. Inspect the impacted files.
3. Map impacted feature IDs from `02_FEATURE_PRESERVATION_MAP.md`.
4. Make the smallest coherent implementation change.
5. Run targeted component tests.
6. Run targeted browser/screenshot tests.
7. Check relevant performance counters or instrumentation.
8. If anything fails, fix it and rerun.
9. Document impacted features and validations in the task note, commit message, or PR note.

## Shared regression requirements

Whenever you touch shared canvas code, also validate:
- PromptFactory browser behavior,
- CanvasWorkbench component tests,
- CanvasFloatingWindow component tests.

## Deliverable standard

A task is only complete when:
- preserved features still work,
- targeted tests are green,
- browser/screenshot gates are green,
- claimed performance wins are evidenced,
- no obvious cross-surface regression remains.

Now execute the tasks in `codex/TASK_SEQUENCE.md` one by one.
