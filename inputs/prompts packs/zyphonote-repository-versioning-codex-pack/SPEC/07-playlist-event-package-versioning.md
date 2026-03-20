# Playlist, event, and package versioning

These three domains are easier than score merge because they already contain stable keys or can gain them cheaply.

## Playlist strategy

### Current strengths
The current playlist manifest already has:
- `sectionKey`
- item `key`
- exact score version references

### Required repository conversion
Future playlist snapshot files should include:
- `playlist/meta.json`
- `playlist/manifest.json`

### Merge rules
Use structured merge by:
- `sectionKey`
- `itemKey`

Auto-merge when:
- different sections were changed independently
- one side edits notes/labels, the other changes timing elsewhere
- independent items were inserted

Conflict when:
- both sides edit the same item properties differently
- both sides move the same item differently
- both sides delete/modify the same section incompatibly

### Reference pinning
Playlist manifest items should pin:
- `scoreCommitHash`

Keep `scoreVersionId` only as a compatibility field during migration.

## Event strategy

### Current gaps
Events do not yet have immutable version history.

### Suggested snapshot files
- `event/meta.json`
- `event/checklist.json`
- `event/links.json`

### Merge rules
Scalar fields:
- field-level three-way merge

Checklist rows:
- stable `checklistItemId`
- auto-merge different items
- conflict when same item edited differently

Linked playlists:
- merge as keyed references
- pin exact playlist commit hashes where appropriate for frozen exports

## Learning package strategy

### Current strengths
Learning packages already have:
- immutable versions,
- content-addressed manifest storage,
- stable section/item keys,
- exact score version linking.

### Required repository conversion
Package repository snapshots should include:
- `package/meta.json`
- `package/manifest.json`

### Merge rules
Use structured merge by:
- section key
- item key

Pin:
- `scoreCommitHash`
- asset ids + asset hashes

## Compatibility bridge rule

During migration:
- keep legacy package/playlist version rows,
- add `commit_hash` mappings,
- keep current screens working,
- progressively switch reads to repository refs.

## Share/publish implications

### Playlist share
A share must point to:
- exact playlist commit hash

### Package publish
A published package/listing must point to:
- exact package commit hash

### Event exports
Any future public/event export should point to:
- exact event commit hash
- exact linked playlist commit hashes if included

This avoids mutable history surprises.
