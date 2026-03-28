# Performance budgets and acceptance

These budgets are engineering gates, not public product promises.

They exist to keep the refactor honest and to force evidence-driven decisions.

## Graph tiers for validation

### Tier A
- ~200 nodes
- ~250 links

### Tier B
- ~500 nodes
- ~700 links

### Tier C
- ~1000 nodes
- ~1400 links

The exact fixture may vary, but all performance comparisons must use a deterministic seeded graph so before/after numbers remain meaningful.

## Hot-path interaction budgets

### Pan
- Must remain client-local during active movement.
- Must not cause `SaveViewStateAsync` on every intermediate state.
- After P1, pan should not require full node/link layer teardown.

### Single-node drag
- Must remain client-local until drop.
- Must not trigger N service calls.
- Must not force a full surface reload for the drag loop itself.

### Multi-node drag
- One batch mutation call per drag commit.
- One DB transaction per drag commit.
- After P1, only moved nodes and affected links/guides should patch during drag.

### Floating-window drag/resize
- Zero persisted state writes during active movement.
- At most one persisted state write on commit.
- No scene zoom caused by wheel input inside scrollable window content.

### Selection changes
- No full `ReloadSurfaceAsync()` for simple selection changes.
- Only the minimal overlay/UI sync path should run.

### Simple node-property mutations
The following must avoid full structure reload after P0/P1 unless there is a proven dependency that requires it:
- status,
- progress,
- marker,
- priority,
- note text edit,
- selection-border updates.

## Renderer budgets

## Forbidden hot-path pattern after P1
These paths must not clear full scene layers during normal interactions:
- pan,
- node drag,
- frame drag,
- selection highlight,
- hover transitions,
- overlay movement,
- toolbox interaction.

## Required retained-mode counters
The renderer should expose counters or diagnostics for:
- total render passes,
- full node-layer rebuild count,
- full link-layer rebuild count,
- patched node count,
- patched link count,
- culling visible node count,
- state publish count.

## Server and persistence budgets

### View-state persistence
- No DB writes during active pan/zoom.
- No DB writes during active floating-window movement.
- Persist view state only after commit/idle.

### Structure reloads
- No `ReloadSurfaceAsync()` for view-only operations.
- No full `GetStructureAsync()` after every status/progress/marker/priority edit once local patching exists.

### Graph sync
- `SyncGraphAsync()` should be reserved for structural invalidation or explicit refresh paths, not viewport churn.

## Acceptance checklist

A task is not done until all of the following are true:

- [ ] Impacted features from `02_FEATURE_PRESERVATION_MAP.md` were identified.
- [ ] Relevant unit/bUnit tests pass.
- [ ] Relevant Playwright/browser tests pass.
- [ ] Required screenshots/artifacts were produced and reviewed.
- [ ] Instrumentation or counters show the expected improvement.
- [ ] Shared-canvas regressions (PromptFactory/Sandbox when applicable) were checked.
- [ ] New source comments remain in English.
- [ ] No unexplained behavior regression remains open.

## Stretch goals (not required for initial acceptance)

- Tier C graph remains comfortably navigable.
- Scene patch counts remain much smaller than total graph counts during local drag.
- Large-graph toolbox and overlay usage feels unaffected by scene density.
