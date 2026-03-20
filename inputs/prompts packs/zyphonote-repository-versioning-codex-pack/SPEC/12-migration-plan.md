# Migration plan

## Principle

Do not do a big-bang replacement of the current version tables.

Use a bridge migration with backfill and dual-write.

## Phase 0 – preparation

Add:
- repository schema
- storage roots
- hashing helpers
- repository service layer
- verification tool
- backfill tool

Do not switch UI yet.

## Phase 1 – backfill existing histories

### Scores
Backfill each `score_versions` chain into:
- one repository
- one `main` branch
- one commit per version row ordered by `created_utc`

### Learning packages
Backfill each `learning_package_versions` chain the same way.

### Playlists
Backfill each `playlist_plan_versions` chain the same way.

### Events
Create one repository per event with:
- one initial commit from current relational state

Historical event versions cannot be reconstructed if they never existed; document that honestly.

## Phase 2 – dual-write

When saving:
- continue legacy version rows
- also create repository commits
- store commit hash mapping on legacy version rows

This keeps old pages working while new UI/API rolls out.

## Phase 3 – read from refs

Switch read paths gradually:

### First
- graph UI reads repository data
- current screens still load legacy detail payloads where needed

### Then
- detail editors load from default branch commit tree
- legacy version rows become compatibility/audit bridge only

## Required DB bridge columns

Add to entity roots:
- `repository_id`
- `current_commit_hash`
- `published_commit_hash` where relevant

Add to legacy version tables:
- `commit_hash`

Add to purchases/shares:
- exact commit hash columns

## Read model refresh rules

When `main` moves:
- refresh entity read model fields

When non-default branch moves:
- update repo metadata only
- do not overwrite published/current read model fields

## Backfill tool requirements

`tools/repo-backfill.php` should:
- be restartable/idempotent
- skip already-linked rows safely
- verify content hashes
- log any mismatch
- create audit entries or a structured report

## Rollback stance

Because this is additive:
- old tables remain
- rollback is mostly disabling new UI/API paths if needed
- repository data stays available for debugging

## Completion condition for migration

Migration is complete only when:
- all existing score/package/playlist roots have repository ids
- all new writes produce repository commits
- event repositories exist
- graph UI works on all four domains
- purchases/shares pin commit hashes
- tests cover the backfill and new write flow
