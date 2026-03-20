# Executive summary

## What Zyphonote needs

Zyphonote now needs a **shared repository engine** that behaves like a simplified Git origin for musical content and planning content.

The system must support:

- immutable commits,
- branch tips,
- merge commits,
- forks,
- merge requests,
- offline clone/push/pull for WASM,
- a repository graph in the PHP web UI,
- exact commit pinning for purchases, shares, and published snapshots.

## What was missing from the initial requirement

These are the important missing details that must be designed now, not later:

1. **Stable identifiers inside score documents**
   - Without stable ids for measures / voices / notes, structured merge is weak.
2. **Canonical serialization**
   - PHP and C# must hash the same bytes for the same logical content.
3. **Read model vs source of truth**
   - Existing DB entity tables should become read models, not the entire truth.
4. **Protected branch rules**
   - `main` cannot be deletable/force-pushable in the first release.
5. **Fork policy**
   - Public score/package repos can allow forks; private event/playlist repos should default to owner-only forks.
6. **Rights/legal policy for forks**
   - A forked commercial score/package must not automatically become sellable.
7. **Purchase/share pinning**
   - Purchases and public review shares must point to exact commit hashes.
8. **Garbage collection**
   - Content-addressed stores need safe cleanup of unreachable blobs later.
9. **Backfill strategy**
   - Existing score/package/playlist versions must be imported into the new graph.
10. **Offline object storage**
    - Use IndexedDB/OPFS for repository objects; use localStorage only for tiny UI state.
11. **Audit and repair tools**
    - You need verification tools for hash mismatches and rebuilds.
12. **Non-default branch read behavior**
    - Editing a side branch must not overwrite the public/main read model.

## Recommended architecture in one sentence

**Use a generic repository core (blobs + snapshots + commits + refs + merge requests) and keep existing entity tables as compatibility/read-model bridges.**

## Entity scope

### Must use repository core
- score
- learning_package
- playlist
- event

### Must remain bridge/read model
- `scores`
- `learning_packages`
- `playlist_plans`
- `performance_events`
- related marketplace/share/purchase tables

## Recommended release order

### Phase 1
- repository storage + DB model
- backfill
- score/package/playlist dual-write
- event repositories
- read-only graph UI
- branch creation / commit / fast-forward merge
- API compare endpoints
- fork + MR backend

### Phase 2
- write actions from PHP pages
- richer compare UX
- structured score merge preview
- fork/MR owner workflows in UI
- full WASM local clone/push/pull

### Phase 3
- interactive score merge UI
- comments/reviews on merge requests
- collaborator permissions
- garbage collection and object repair utilities

## Non-goals for the first iteration

- full Git feature parity
- force push
- rebase UI
- cherry-pick UI
- commit signing
- binary diff
- multi-user live collaborative editing

Those can come later without breaking the repository core.
