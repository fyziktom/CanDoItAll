# Unified repository domain model

## Main objects

### Repository
A logical root for one versioned entity.

Fields:
- `repository_id`
- `entity_type`
- `root_entity_id`
- `owner_user_id`
- `default_branch_name`
- `upstream_repository_id`
- `fork_policy`
- `visibility`
- timestamps

### Blob
One immutable file payload, identified by SHA-256 and optionally CID v1 raw.

Fields:
- `sha256`
- `cid_v1_raw`
- `storage_key`
- `size_bytes`
- `mime_type`
- `content_kind`

### Snapshot
One immutable tree manifest that lists repo paths -> blob hashes.

Fields:
- `snapshot_hash`
- `cid_v1_raw`
- `storage_key`
- `entry_count`
- `manifest_size_bytes`

### Commit
One immutable history node.

Fields:
- `commit_hash`
- `repository_id`
- `snapshot_hash`
- `message`
- `author_user_id`
- `authored_utc`
- `committed_utc`
- `commit_kind`
- `payload_storage_key`
- `metadata_json`

### Parent edge
A commit can have:
- one parent (normal commit)
- two parents (merge commit)

### Ref
A movable named pointer.

Types:
- `branch`
- `tag`
- `published`

Important fields:
- `name`
- `tip_commit_hash`
- `is_protected`
- `is_default`

### Merge request
Tracks a proposed merge from one branch/repo to another.

Fields:
- `source_repository_id`
- `source_branch_name`
- `source_head_commit_hash`
- `target_repository_id`
- `target_branch_name`
- `target_head_commit_hash`
- `merge_base_commit_hash`
- `title`
- `description`
- `status`
- `mergeable_state`
- `merge_strategy`
- `merged_commit_hash`

## Domain invariants

1. A blob is immutable and keyed by content hash.
2. A snapshot is immutable and keyed by canonical manifest hash.
3. A commit is immutable and keyed by canonical commit payload hash.
4. A ref is mutable and can move only through controlled updates.
5. Protected refs cannot be force-pushed or deleted in v1.
6. The default branch tip is the branch that updates the read model.
7. Purchases/shares/published states pin exact commit hashes.
8. Merge requests are snapshots of source/target heads at evaluation time and must be revalidated before merge.

## Recommended repository settings

### Score
- `fork_policy = same_owner_only` while private/unlisted
- `fork_policy = public` only when explicitly allowed by rights policy

### Learning package
- same as score

### Playlist
- `fork_policy = same_owner_only`

### Event
- `fork_policy = same_owner_only`

This avoids accidental exposure of private planning data.

## Ref naming conventions

Recommended:
- `main`
- `feature/<slug>`
- `draft/<slug>`
- `review/<slug>`
- `fork-sync/<slug>`
- `published/<yyyy-mm-dd-hhmmss>` as immutable tag-like ref

## Commit kinds

Use an explicit `commit_kind` field:

- `commit`
- `merge`
- `import`
- `publish`
- `sync`
- `backfill`
- `system`

This helps UI labeling and audit filters.

## Read-model bridge rule

For each entity table:
- store `repository_id`
- store `current_commit_hash`
- optionally store `published_commit_hash`
- keep legacy `current_version_id` during transition

For each legacy version table:
- add `commit_hash`
- add `snapshot_hash` if useful
- keep the old primary key until the repo migration is stable

## Audit requirements

Every mutation should write audit entries for:
- create branch
- delete branch
- move ref
- commit
- merge
- fork
- create MR
- close MR
- merge MR
- protected-branch rejection
