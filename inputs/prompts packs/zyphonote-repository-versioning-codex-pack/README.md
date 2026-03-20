# Zyphonote Repository Versioning / Forks / Merge Requests – Codex implementation pack

This pack is tailored to the current **`zyphonote-web-ui-refresh`** PHP 8.2 + MariaDB repository.

It introduces a **unified git-like repository layer** for the following root entities:

- scores,
- learning packages / bundles,
- playlists,
- events.

The design is intentionally **offline-first** and **origin-centric**:

- the server is the source of truth,
- the WASM Blazor client can clone, branch, commit, merge, fork, and sync,
- the PHP web app gains the same repository graph visibility and management endpoints,
- content storage becomes **content-addressed** instead of mostly **version-id-addressed**.

## Verified findings from the current repo

The current repository already contains important building blocks, but they are inconsistent:

- **Scores** already have immutable version rows and stored files, but the file path is based on `scoreId/versionId`, not on content hash.
  - `src/lib/user-auth.php`
  - `src/api/v1/index.php`
- **Playlists** already have immutable version rows and stored manifest files, but the storage key is also based on version id, not hash.
  - `src/lib/planning.php`
- **Learning packages** already use a much better pattern:
  - immutable versions,
  - manifest hashing,
  - optional CID v1 raw,
  - content-addressed manifest storage by CID / SHA.
  - `src/lib/learning-packages.php`
- **Events** currently have no immutable version history at all.
  - `src/lib/planning.php`
- The current API has **simple score sync**, but not repositories, branches, forks, merge requests, or reusable compare/merge endpoints.
  - `src/api/v1/index.php`
- The UI already has reusable **canvas / workbench chrome** that should be reused for a GitKraken-like repository graph.
  - `src/assets/js/zy-canvas-workbench.js`
  - `src/assets/js/zy-learning-pack-canvas.js`
  - `src/dev-canvas-gallery.php`

## What this pack delivers

- a detailed architecture for a **generic repository core**,
- a migration path that does **not require a big-bang rewrite**,
- a fix for the current hashing/storage inconsistency,
- a realistic merge model for music scores,
- fork + merge request support,
- PHP + API + WASM integration guidance,
- proposed SQL migration,
- OpenAPI contract,
- code skeletons,
- checklists,
- sequenced Codex prompts,
- seed/test guidance.

## Recommended usage

1. Give Codex:
   - the current repository,
   - this pack.

2. Start with:
   - `START_PROMPT.md`

3. Make Codex execute every prompt in `PROMPTS/` in order.

4. Do not let Codex declare completion until every checklist item is either:
   - implemented,
   - explicitly justified as deferred,
   - or replaced by a safe equivalent.

## Important product/technical stance

The best path for Zyphonote is **not** to bolt branches onto the current per-entity version tables directly.

Instead:

- keep existing entity tables as **read models** and compatibility bridges,
- add a **generic repository domain** shared by scores, packages, playlists, and events,
- store blobs / snapshots / commits in a **content-addressed object store**,
- use branches / commits / merge requests on top of that shared layer,
- let PHP and WASM both consume the same repository APIs.

That gives you:

- consistent hashes,
- offline clone/push/pull,
- graph rendering,
- exact version pinning for purchases and shares,
- future merge tools without another redesign.
