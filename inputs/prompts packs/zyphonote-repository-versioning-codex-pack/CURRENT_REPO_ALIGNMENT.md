# Current repository alignment

This document maps the pack to the **actual current repo** and highlights the already-verified behavior that Codex must preserve or fix.

## 1. Scores – already versioned, but storage is not content-addressed

### Verified files
- `src/lib/user-auth.php`
  - `zy_user_auth_create_musicxml_score(...)`
  - `zy_user_auth_save_score_document(...)`
- `src/api/v1/index.php`
  - `v1_scores_save_version(...)`
  - `v1_save_score_document(...)`

### Verified behavior
- A score save creates a new version row.
- The stored file path is based on:
  - `scoreId`
  - `versionId`
- SHA-256 is computed and stored in DB.
- The file name is **not** derived from the content hash.
- Saving identical content twice still creates another stored file because the storage key changes with version id.

### Consequence
- You get immutable versions, but not real content-addressed dedupe.
- This is good enough for simple history, but not for a generic Git/IPFS-like object model.

## 2. Score sync – currently optimistic conflict handling only

### Verified files
- `src/api/v1/index.php`
  - `v1_sync_pull(...)`
  - `v1_sync_push(...)`
  - `v1_sync_apply_score_document(...)`

### Verified behavior
- The current API supports score pull/push only.
- Conflict detection is based on:
  - `baseVersionId`
  - server `current_version_id`
- There are no:
  - branches,
  - forks,
  - merge requests,
  - compare endpoints,
  - reusable commit graph endpoints.

### Consequence
- This is a useful seed for offline sync, but it is not enough for Git-like origin management.

## 3. Playlists – immutable versions exist, but still linear and version-id-addressed

### Verified files
- `src/lib/planning.php`
  - `zy_planning_storage_key_for_version(...)`
  - `zy_planning_write_version(...)`
  - `zy_planning_save_draft_manifest_internal(...)`

### Verified behavior
- Playlist manifests are saved as immutable version rows.
- Storage key is derived from `playlistVersionId`.
- SHA-256 is stored.
- The model is linear (`version_no`), not graph-based.
- There is no repository abstraction.

### Consequence
- Playlists already have a solid immutable-save pattern.
- They are a good candidate for migration into the generic repository core.
- The existing tables should become bridge/read-model tables.

## 4. Events – no immutable repository history yet

### Verified files
- `src/lib/planning.php`
  - `zy_planning_create_event(...)`
  - `zy_planning_update_event(...)`

### Verified behavior
- Event data is stored in normal relational tables.
- Checklist items are stored relationally.
- There is no immutable event snapshot/version chain.

### Consequence
- Events need a repository added from scratch.
- Existing DB rows should remain the searchable current read model.

## 5. Learning packages – best current reference implementation

### Verified files
- `src/lib/learning-packages.php`
  - `zy_learning_save_draft_manifest_internal(...)`
  - `zy_learning_storage_key_for_cid(...)`
  - `zy_learning_compute_cid_v1_raw(...)`

### Verified behavior
- Package manifests are immutable version snapshots.
- SHA-256 is computed.
- CID v1 raw can be computed.
- Storage key is content-addressed by CID/SHA.
- If the same content already exists on disk, the file can be reused.

### Consequence
- This is the current best pattern in the repo.
- The new generic repository blob/snapshot store should mirror this style.

## 6. UI foundations already exist for the graph/workbench

### Verified files
- `src/assets/js/zy-canvas-workbench.js`
- `src/assets/js/zy-learning-pack-canvas.js`
- `src/dev-canvas-gallery.php`
- `src/assets/js/clay-shell.js`

### Verified behavior
- The repo already has:
  - ribbon/workbench chrome,
  - dock panels,
  - zoomable canvas patterns,
  - a Canvas Lab for experimentation.

### Consequence
- The repository graph should reuse these patterns instead of introducing an unrelated UI style.

## 7. Current PHP pages that should host repository graphs

### Primary targets
- `src/account-score-detail.php`
- `src/account-dashboard.php` / `src/account-my-scores.php`
- `src/account-playlists.php`
- `src/account-events.php`
- `src/account-learning-builder.php`
- optionally `src/account-learning-package.php`

### Why
- These pages already represent the owner-facing edit/view surface for the four entity roots.
- They are the correct integration points for:
  - branch status,
  - commit history,
  - merge actions,
  - fork / MR entry points,
  - compare / conflict preview hooks.

## 8. Current API structure warning

### Verified file
- `src/api/v1/index.php`

### Observation
- The file is already large and contains many unrelated domains.

### Required implementation stance
- Codex should keep the front controller route registration there if needed,
- but move repository domain logic into shared lib files such as:
  - `src/lib/repositories.php`
  - `src/lib/repository-score.php`
  - `src/lib/repository-api.php`
  - `src/lib/repository-ui.php`

## 9. Storage config alignment

### Verified file
- `src/lib/config.example.php`

### Existing roots already present
- `scores_root`
- `learning_manifests_root`
- `planning_manifests_root`

### Required additions
Add repository-oriented roots such as:
- `repositories_root`
- `repository_blobs_root`
- `repository_snapshots_root`
- `repository_commits_root`

Keep the path resolution style consistent with the existing config conventions.

## 10. Core conclusion

The current repo is already partially prepared for this feature:

- immutable saves already exist in three places,
- content-addressed manifests already exist for learning packages,
- offline sync concepts already exist for scores,
- canvas workbench UI already exists.

The correct strategy is therefore:

1. add a generic repository core,
2. backfill existing score/package/playlist histories into it,
3. create event repositories,
4. dual-write during migration,
5. switch PHP and WASM reads to repository refs/tips incrementally.
