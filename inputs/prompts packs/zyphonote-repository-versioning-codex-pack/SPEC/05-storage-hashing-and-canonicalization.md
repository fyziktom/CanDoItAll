# Storage, hashing, and canonicalization

## Verified current problem

### Scores
Current score storage uses:
- `scoreId/versionId.json`

### Playlists
Current playlist storage uses:
- file path derived from `playlistVersionId`

That means:
- you do get new immutable files per version,
- but you do **not** get dedupe by content hash.

## Required fix

Split the problem into three hash layers:

1. **Blob hash**
   - hash of one stored file payload
2. **Snapshot hash**
   - hash of the canonical file-tree manifest
3. **Commit hash**
   - hash of the canonical commit payload

## Canonicalization rules

The same logical content must hash to the same bytes in PHP and C#.

### Text files
- UTF-8, no BOM
- normalize line endings to `\n`

### Canonical JSON
- object keys sorted ascending
- arrays keep semantic order
- no insignificant whitespace
- booleans/numbers normalized consistently
- emit UTF-8 bytes directly

### Binary assets
- hash raw bytes directly

## Why canonical JSON matters

Without canonicalization:
- PHP may encode keys in one order,
- C# may encode them in another,
- the same logical commit would hash differently,
- offline commits would not verify cleanly on the server.

## Recommended file layout per snapshot

### Score
- `score/meta.json`
- `score/source.musicxml`
- `score/canonical.json`
- `score/render.json` (optional derived cache)

### Package
- `package/meta.json`
- `package/manifest.json`

### Playlist
- `playlist/meta.json`
- `playlist/manifest.json`

### Event
- `event/meta.json`
- `event/checklist.json`
- `event/links.json`

## Blob dedupe rule

When saving a file:
1. canonicalize bytes if needed,
2. compute SHA-256,
3. compute CID v1 raw if configured,
4. derive content-addressed storage key,
5. if blob already exists:
   - reuse it,
   - do not write duplicate disk content.

## Snapshot dedupe rule

A new commit may reuse an existing snapshot hash if the file tree is identical.

That means:
- new commit metadata can exist,
- while the underlying snapshot tree stays shared.

## Commit dedupe rule

By default, reject empty commits where:
- parent tree == new tree

Exceptions:
- explicit allowed empty merge/system commits if product really needs them.

## Recommended helper functions

Shared helpers should exist for:
- canonical JSON encode
- text normalization
- SHA-256 hex
- CID v1 raw from SHA-256
- storage key from hash/CID
- blob persistence with reuse
- snapshot manifest encode + hash
- commit payload encode + hash

## Verification tooling

Add command-line utilities:
- `tools/repo-verify.php`
- `tools/repo-backfill.php`
- `tools/repo-gc.php`

### `repo-verify.php`
Should verify:
- blob file exists for DB row
- blob file hash matches DB hash
- snapshot manifest file exists and matches DB hash
- commit payload exists and matches commit hash
- refs point to existing commits

## Recommended config additions

Add config/env support for:
- `REPOSITORIES_STORAGE_ROOT`
- `REPO_BLOBS_ROOT`
- `REPO_SNAPSHOTS_ROOT`
- `REPO_COMMITS_ROOT`

Follow the same path resolution conventions already used in current config.
