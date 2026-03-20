# Target architecture

## Core idea

Every versioned root entity becomes a **repository**.

Each repository contains:

- blobs (content-addressed file payloads),
- snapshots (content-addressed file trees/manifests),
- commits (immutable history nodes),
- refs (branch/tag pointers),
- optional merge requests.

## Read model split

### Source of truth
- repository blobs/snapshots/commits/refs on server storage + DB metadata

### Read models
- existing entity tables used for:
  - search/filter lists,
  - dashboard summaries,
  - marketplace cards,
  - planning tables,
  - moderation screens,
  - compatibility with current code paths

This separation is necessary because:
- MariaDB is good for queryable summaries,
- object storage is better for immutable snapshots and hashing,
- the WASM client needs cloneable immutable history.

## Repository-per-entity rule

### Score repository
One repository per score root.

Default branch:
- `main`

Suggested snapshot files:
- `score/meta.json`
- `score/source.musicxml`
- `score/canonical.json`
- `score/render.json` (optional derived cache)

### Learning package repository
One repository per package root.

Suggested snapshot files:
- `package/meta.json`
- `package/manifest.json`

### Playlist repository
One repository per playlist root.

Suggested snapshot files:
- `playlist/meta.json`
- `playlist/manifest.json`

### Event repository
One repository per event root.

Suggested snapshot files:
- `event/meta.json`
- `event/checklist.json`
- `event/links.json`

## Why snapshot trees instead of a single raw JSON field

Because the future system needs:

- Git-like file-level compare,
- dedupe of unchanged companion files,
- exact hashing of full entity state,
- reuse between PHP and WASM,
- future export/import workflows.

## Default branch and read-model rule

The entity read model should represent the **default branch tip** only.

That means:
- branch `main` updates the read model,
- side branches do not overwrite public/current entity fields,
- a merge into `main` updates the read model.

This avoids breaking the current marketplace/planning assumptions.

## Publish/share/purchase pinning

### Published score/package
Store exact:
- `published_commit_hash`

### Purchased score/package
Store exact:
- `purchased_commit_hash`

### Playlist share
Store exact:
- `shared_commit_hash`

This prevents “moving target” history bugs.

## Repository lifecycle

### Create
- create entity row
- create repository
- create initial snapshot
- create initial commit
- create `main` branch ref

### Commit
- persist missing blobs
- persist snapshot
- persist commit
- update target branch tip with compare-and-swap

### Merge
- compute merge base
- produce merge preview
- if clean, create merge commit
- update target branch tip
- if target is default branch, refresh read model

### Fork
- create new repository linked to upstream
- copy chosen branch tip into new fork default branch
- preserve upstream reference

## Storage layout recommendation

### Server object storage
- `storage/api/repos/blobs/ab/cd/<sha256>`
- `storage/api/repos/snapshots/ab/cd/<snapshotHash>.json`
- `storage/api/repos/commits/ab/cd/<commitHash>.json`

### Why
- consistent with current disk-storage style,
- content-addressed,
- easy to verify,
- easy to back up,
- easy to clone by hash.

## Recommended new shared PHP modules

- `src/lib/repositories.php`
- `src/lib/repository-hashing.php`
- `src/lib/repository-score.php`
- `src/lib/repository-planning.php`
- `src/lib/repository-learning.php`
- `src/lib/repository-events.php`
- `src/lib/repository-api.php`
- `src/lib/repository-ui.php`

Keep route registration in `src/api/v1/index.php`, but move implementation to shared libs.
