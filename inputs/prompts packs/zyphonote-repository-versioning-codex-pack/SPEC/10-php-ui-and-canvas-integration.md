# PHP UI and canvas integration

## UX goal

The repository UI should feel like a first-class Zyphonote authoring surface, not a hidden admin table.

## Reuse existing UI foundations

Build on:
- `src/assets/js/zy-canvas-workbench.js`
- `src/assets/js/zy-learning-pack-canvas.js`
- `src/dev-canvas-gallery.php`

Do not introduce a visually unrelated graph widget.

## Recommended new assets

- `src/assets/js/zy-repository-graph-canvas.js`
- `src/assets/js/zy-repository-graph-page.js`
- `src/lib/repository-ui.php`

## Recommended graph panel layout

### Main canvas
- commit graph lanes
- commit nodes
- branch labels
- merge arcs
- fork/upstream badges
- ahead/behind badge

### Left dock
- repository summary
- branch list
- quick actions:
  - fetch
  - create branch
  - fork
  - compare
  - create MR

### Right dock
- selected commit inspector
- changed files summary
- merge preview summary
- downstream impact:
  - linked playlists/packages/shares if available

## Graph rendering rules

To feel GitKraken-like:
- newest commits at top
- lane colors stable during one render session
- branch labels shown on tip commits
- merge arcs curved and readable
- selected node visibly emphasized
- large history paginated/lazy-loaded
- hover tooltip shows short hash, message, author, time

## Lane assignment recommendation

Client-side algorithm:
1. topologically sort visible commits by commit time desc, honoring ancestry
2. assign lanes from active branch tips
3. reuse freed lanes after merges
4. preserve lane identity while still visible

Server may optionally return lane hints, but the client should be able to recompute them for offline use.

## File-by-file integration targets

### Score flows
- `src/account-score-detail.php`
- `src/account-dashboard.php` / `src/account-my-scores.php`

Add:
- repository graph card
- branch selector
- commit action modal
- compare/merge/fork/MR entry points
- score-specific change summary panel

### Playlist flows
- `src/account-playlists.php`

Add:
- repository graph beside builder/manifest editor
- branch status above save/share actions
- diff summary for current branch vs main

### Event flows
- `src/account-events.php`

Add:
- repository graph for event changes
- branch awareness when editing checklist/logistics

### Learning package flows
- `src/account-learning-builder.php`
- optionally `src/account-learning-package.php`

Add:
- repository graph for authoring history
- compare against published branch/tag
- fork/MR actions where allowed

## Interaction rules

### Commit
- no staging
- commit all dirty changes
- require message
- show target branch

### Branch switch
- warn if working copy dirty
- allow local-only branch in WASM later
- in PHP phase, only switch server branch context for that view/session

### Merge
- if clean: allow merge action
- if conflict: show summary + open compare details
- full rich per-hunk editor can come later

## Accessibility
- keyboard navigation between nodes
- inspector readable without hover
- not canvas-only for critical information:
  - provide textual commit list fallback or inspector list

## Empty/read-only states
- if repo not backfilled yet:
  - show “History is being migrated” placeholder
- if user only has read access:
  - hide mutation actions
  - still show graph if visibility allows

## CSS/JS integration principle
Keep the graph module self-contained and reusable across all four entity pages.
