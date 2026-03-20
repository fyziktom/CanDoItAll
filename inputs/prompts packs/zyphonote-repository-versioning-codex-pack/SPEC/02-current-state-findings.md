# Current state findings

## Verified entity-by-entity summary

| Entity | Current immutable versions | Stored files | Hash stored | Content-addressed storage | Branches | Forks/MRs | API support |
|---|---:|---:|---:|---:|---:|---:|---:|
| Score | Yes | Yes | Yes | No | No | No | Partial |
| Learning package | Yes | Yes | Yes | Yes | No | No | Partial |
| Playlist | Yes | Yes | Yes | No | No | No | No public API |
| Event | No | No | No | No | No | No | No public API |

## Score – exact finding

### What is true now
- New score versions are immutable.
- New stored file paths are created per save because the storage key includes the new version id.
- SHA-256 is computed over the stored content bytes.

### What is not good enough
- The content hash is not the storage identity.
- The same content can be stored multiple times under different version ids.
- The current sync model detects only head mismatch, not graph divergence.

### Required correction
- Split:
  - blob identity,
  - snapshot identity,
  - commit identity.
- Reuse blobs by hash even when a new commit is created.
- Keep commit metadata separate from file blob identity.

## Learning package – exact finding

### What is true now
- Package manifests already follow the best storage pattern in the repo.
- CID-ready storage exists already.

### Why this matters
- This is the strongest proof that the repo can support content-addressed storage on the same hosting stack.

### Required action
- Reuse this style in the generic repository core.
- Do not leave package versioning as a completely separate long-term system.

## Playlist – exact finding

### What is true now
- Playlist manifests are already immutable snapshots.
- They already link exact score versions, which is the correct historical principle.

### What is missing
- No graph.
- No branches.
- No merge.
- No fork/MR flow.
- No content-addressed store.

### Required action
- Migrate playlist versions into repository commits.
- Keep current playlist version rows as a compatibility mapping table or bridge.

## Event – exact finding

### What is true now
- Events are currently mutable relational rows only.
- No immutable version history exists.
- Checklist items are mutable.

### Required action
- Add immutable event snapshot commits from day one.
- Event checklist rows need stable ids and structured merge rules.

## Important cross-domain finding

Learning packages and playlists already show the right long-term historical behavior:

- their snapshots pin exact score versions,
- not just “latest score”.

Under the new repository model this becomes:

- package manifests pin exact `scoreCommitHash`,
- playlist manifests pin exact `scoreCommitHash`,
- purchases and shares pin exact package/playlist commit hashes.

This is critical for reproducibility.

## Final recommendation from the audit

Do **not** attempt to extend each entity’s current version table independently.

Instead:

- add one repository domain,
- add entity-specific bridges into it,
- migrate/backfill existing versions,
- then gradually move reads and writes to refs/commits.
