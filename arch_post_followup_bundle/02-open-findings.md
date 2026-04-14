# Open findings

This document records the still-open issues that justify this follow-up.

## F001 — Process graph legality is not yet enforced

### Evidence
- `src/CanDoItAll.Modules.Processes/ProcessesService.Support.cs:11-72`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs:124-130`
- `src/CanDoItAll.Modules.Processes/ProcessCanvasRecompositionService.cs:401-403`

### Why this matters
The Process module now uses a canonical dependency representation, but it still does **not** guarantee that the graph is a legal DAG. Self-loops and multi-node cycles are still not rejected explicitly.

`StartRunAsync` still falls back to the first step when no roots exist, which means an illegal cyclic graph can still be run with arbitrary semantics instead of being rejected. The canvas recomposition path still appends unresolved nodes after the topological pass, which silently masks illegal ordering instead of surfacing it.

### Required closure bar
- Save/publish reject cycles and self-loops.
- Runtime start has no “pick the first step anyway” fallback.
- Canvas topological ordering no longer silently tolerates illegal graphs.

## F002 — Runtime schema singularity is still weaker than runtime service assumptions

### Evidence
- `src/CanDoItAll.Modules.Processes/ProcessRuntimeEntityConfigurations.cs:55-58`
- `src/CanDoItAll.Modules.Processes/ProcessRuntimeEntityConfigurations.cs:85`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs:243-244`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.Operations.cs:20-25`

### Why this matters
The service code assumes one `ProcessStepRun` per `(ProcessRunId, StepDefinitionId)` and one logical `ProcessRunAssignment` per `(ProcessRunId, RoleRequirementId, StepDefinitionId?)`, but the schema still does not fully enforce those assumptions.

This is not just theoretical. `ResolveAssignmentAsync` has a real concurrent duplicate-insert race because it reads with `FirstOrDefaultAsync` and inserts without a unique index protecting the logical key.

### Required closure bar
- DB-backed uniqueness for step runs per run+step definition.
- DB-backed uniqueness for run assignments at both run scope and step scope.
- Tests proving duplicate runtime rows are rejected.

## F003 — ProcessWorkspace still has pending-autosave ordering bugs

### Evidence
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.DefinitionCrud.cs:38-127`
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.Persistence.cs:5-54`
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.Persistence.cs:70-110`

### Why this matters
`SaveAsync` already cancels and drains pending definition persistence correctly. `PublishAsync`, `DeleteAsync`, and `ExportAsync` still do not.

That means:
- publish can run against stale persisted state while local canvas changes are still pending;
- export can serialize stale DB state instead of the current editor state;
- delete can race an in-flight autosave that may recreate the deleted definition under a new identity if the autosave reaches `SaveAsync` after the original row is gone.

This is the strongest remaining cross-thread / action-order issue in the current Process UI.

### Required closure bar
- publish/delete/export all quiesce pending definition persistence first;
- no stale publish, stale export, or delete/autosave recreate race remains;
- tests prove the ordering behavior.

## F004 — Published-only editor path still misses definition stale-write protection

### Evidence
- `src/CanDoItAll.Modules.Processes/ProcessesService.cs:47-63`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Persistence.cs:48-49`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Support.cs:79-80`

### Why this matters
When `GetEditorAsync` returns an editor for an existing definition that currently has no working draft, it still omits `DefinitionConcurrencyToken`. Save conflict detection only fires when the expected token is present.

That leaves a stale-save hole in the published-only/no-draft path.

### Required closure bar
- editor loading always returns `DefinitionConcurrencyToken` for existing definitions;
- a direct integration test proves stale save is rejected in the no-draft path.

## F005 — Workspace reads are still too chatty and not cohesively consistent

### Evidence
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunDetailsLoader.cs:5-20`
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor.cs:176-232`

### Why this matters
Workspace and run-details loading still stitch the UI from many sequential service calls, each of which can use a different `DbContext`. That increases latency and allows torn reads when data changes between calls.

### Required closure bar
- a cohesive read-model/query boundary for run details at minimum;
- ideally a clearer workspace-level read boundary as well.

## F006 — Template helper isolation is still incomplete

### Evidence
- `src/CanDoItAll.Modules.Processes/ProcessTemplateCatalogService.cs:110-154`
- `src/CanDoItAll.Modules.Processes/ProcessTemplateLibraryService.cs:63-112`
- `src/CanDoItAll.Modules.Processes/ProcessTemplateProjectionService.cs:73-101`
- `src/CanDoItAll.Modules.Processes/ProcessTemplatePackLoader.cs:14-20`
- `src/CanDoItAll.Modules.Processes/ProcessTemplatePackModels.cs:5-38`

### Why this matters
Cross-module helper duplication is much better than before, but the Process template subsystem still repeats role/artifact mapping rules across multiple services. Also, the pack loader is scoped and thread-safe within scope, but the pack graph is still mutable, so broader caching/singleton optimization is not safely explicit.

### Required closure bar
- one owner for template-to-editor mapping rules;
- explicit decision: keep pack scoped because mutable, or make it immutable before wider caching.

## F007 — Targeted scale and concentration cleanup is still warranted

### Evidence
- `src/CanDoItAll.Modules.Processes/ProcessesService.Persistence.cs:387-452`
- `src/CanDoItAll.Modules.Processes/ProcessRuntimeProgressionPlanner.cs:65-75`
- `src/CanDoItAll.Modules.Processes/ProcessesService.RuntimeReadQuery.cs:257-329`
- `src/CanDoItAll.Modules.Processes/ProcessOutbox.cs:572-576`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Support.cs:288-292`

### Why this matters
The current code is much safer than before, but still has several obvious scale/concentration hotspots:

- repeated scans inside differential save loops;
- graph-wide scans inside progression planning;
- analytics aggregated in memory after broad materialization;
- some remaining low-value helper duplication;
- several long files that still merit targeted seam extraction.

### Required closure bar
- targeted complexity reductions and low-risk cleanup after correctness is closed;
- no risky rewrite.
