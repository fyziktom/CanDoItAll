# 06 - Sequential Codex Prompts

Run these prompts in order. Do not merge them into one giant request.

## Prompt 01 - Shell and workbench semantics recovery

### Objective

Upgrade the shell from route-first navigation to a real workstation shell with artifact-aware internal tabs.

### Required reading

- `docs/post-implementation-audit/01-source-input-consolidation.md`
- `docs/post-implementation-audit/02-audit-findings.md`
- `docs/post-implementation-audit/03-repair-specification.md`
- `PromptStudio_Architecture_Package/docs/03-ui-architecture-and-ascii-layouts.md`
- `PromptStudio_Architecture_Package/docs/03a-workbench-tabs-canvas-and-state.md`

### Tasks

1. Refactor the shell so the left rail supports workspace and project-aware navigation.
2. Promote artifact/session tab kinds instead of route-only tabs.
3. Add opened-project and prompt-session tab support.
4. Add overflow and batch tab actions.
5. Keep restore, sleep, and dirty-state behavior working.

### Acceptance criteria

- tabs represent real work items
- opened projects and prompt sessions can reopen in dedicated internal tabs
- the shell no longer feels like a flat route launcher

### Stop condition

Stop only when the shell feels like a real workstation baseline.

## Prompt 02 - Unified project object model and project wizard

### Objective

Introduce the unified project object graph and replace raw project CRUD as the primary authoring UX.

### Required reading

- `docs/post-implementation-audit/03-repair-specification.md`
- `docs/post-implementation-audit/04-recovery-plan.md`
- `PromptStudio_Architecture_Package/docs/02-technical-requirements.md`
- `PromptStudio_Architecture_Package/docs/03a-workbench-tabs-canvas-and-state.md`

### Tasks

1. Implement the shared project-object contract and typed object families.
2. Map existing project entities into the graph.
3. Build a wizard-first project creation and edit flow.
4. Support modal and dedicated-tab variants where appropriate.
5. Keep existing persistence logic but remove raw CRUD as the main user-facing path.

### Acceptance criteria

- project creation is wizard-first
- unified project objects exist and are ready for workbench authoring
- project editing no longer depends on one large direct form

### Stop condition

Stop only when project authoring is guided and the graph model is in place.

## Prompt 03 - Real project structure canvas and real project calendar

### Objective

Replace placeholder workbench wrappers with real engine integrations.

### Required reading

- `docs/post-implementation-audit/02-audit-findings.md`
- `docs/post-implementation-audit/03-repair-specification.md`
- `PromptStudio_Architecture_Package/docs/03a-workbench-tabs-canvas-and-state.md`
- `docs/canvas-playlist-builder/README.md`
- `docs/canvas-playlist-builder/rebuild/blazor-jsinterop-component-plan.md`
- `docs/canvas-events-calendar/README.md`
- `docs/canvas-events-calendar/rebuild/blazor-jsinterop-component-plan.md`

### Tasks

1. Replace `workbenchInterop.js` placeholder behavior with real wrappers.
2. Persist node layout, viewport, selection, and view settings.
3. Implement the grouped hex right-click menu on the structure canvas.
4. Support node creation, linking, and artifact opening through typed C# commands.
5. Support project event navigation through the internal tab workspace.

### Acceptance criteria

- the structure surface is a real canvas
- the calendar is a real calendar
- the workbench can author and navigate project objects

### Stop condition

Stop only when the workbench is no longer a dressed-up list view.

## Prompt 04 - Prompt wizard sessions and flow graph recovery

### Objective

Turn the current Prompt Factory form into a real prompt wizard and visual prompt-flow workspace.

### Required reading

- `docs/post-implementation-audit/03-repair-specification.md`
- `docs/post-implementation-audit/04-recovery-plan.md`
- `PromptStudio_Architecture_Package/docs/03-ui-architecture-and-ascii-layouts.md`
- `PromptStudio_Architecture_Package/prompts/07a-shared-prompt-blocks-and-flow-orchestration.md`

### Tasks

1. Refactor prompt generation into step-based sessions.
2. Open prompt sessions as restorable internal tabs.
3. Add governance UI for shared blocks and flow templates.
4. Support auto-apply rules by prompt type, blueprint, and phase.
5. Surface prompt nodes and branches in the project graph.

### Acceptance criteria

- prompt creation is session-based and guided
- prompt sessions reopen correctly
- branches and lineage are visible
- shared block governance is no longer seed-only

### Stop condition

Stop only when prompt work is visibly workflow-driven instead of form-driven.

## Prompt 05 - Typed resource editors and graph visuals

### Objective

Repair the resource UX and align resource types with the unified project object graph.

### Required reading

- `docs/post-implementation-audit/03-repair-specification.md`
- `PromptStudio_Architecture_Package/docs/03-ui-architecture-and-ascii-layouts.md`

### Tasks

1. Replace generic resource editing with type-specific editors.
2. Keep a shared base flow but customize the form experience by resource type.
3. Add resource visual profiles for the project graph.
4. Keep secret references explicit and safe.
5. Preserve search, activity, and validation metadata.

### Acceptance criteria

- users no longer manage most resources through raw `ConfigJson`
- resource editing feels typed and comfortable
- resource objects are visually distinct in the graph

### Stop condition

Stop only when resource management no longer feels like a generic admin form.

## Prompt 06 - Manager and tuning loop completion

### Objective

Replace the fake tuning lifecycle with a real local adapter and complete the manager-driven refinement loop.

### Required reading

- `docs/post-implementation-audit/02-audit-findings.md`
- `docs/post-implementation-audit/03-repair-specification.md`
- `PromptStudio_Architecture_Package/docs/03b-development-manager-watch-capsules-and-tuning.md`

### Tasks

1. Replace simulated tuning execution with a real local Codex adapter or CLI integration.
2. Support screenshot or clipboard image attachment.
3. Preserve session-token protection and loopback-only access.
4. Keep ready-state, watch-state, and capsule-drift validation intact.
5. Add controlled tests for the repaired flow.

### Acceptance criteria

- tuning requests execute through a real adapter
- screenshot context can be submitted
- ready-for-review means actual watch-ready plus no drift

### Stop condition

Stop only when the tuning loop is trustworthy for real development use.

## Prompt 07 - Final QA hardening and release gate

### Objective

Close the repaired work with evidence, regression protection, and a final audit pass.

### Required reading

- `docs/post-implementation-audit/05-checklists.md`
- `PromptStudio_Architecture_Package/docs/08-checklists.md`
- `PromptStudio_Architecture_Package/docs/09-validation-and-testing-plan.md`

### Tasks

1. Fix the Playwright cleanup failure.
2. Expand coverage for repaired flows.
3. Run unit, integration, component, and Playwright tests.
4. Run a final UX walkthrough against the source-input consolidation.
5. Record any remaining intentional gaps explicitly.

### Acceptance criteria

- all repaired journeys have evidence
- the Playwright project exits cleanly
- the product matches the intended workstation direction closely enough to continue feature work

### Stop condition

Stop only when the repaired product has passed its own audit.
