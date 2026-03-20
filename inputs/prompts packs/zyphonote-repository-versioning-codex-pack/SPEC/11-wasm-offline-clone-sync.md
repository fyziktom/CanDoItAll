# WASM offline clone and sync

## Storage recommendation

### Use IndexedDB (primary)
Store:
- repositories
- refs
- commits
- snapshots
- blobs metadata
- local working copies
- pending ref updates / pending pushes

### Optional use OPFS for large blobs
If browser support is acceptable:
- store large raw payloads in OPFS
- keep IndexedDB metadata/index only

### Use localStorage only for tiny UI state
Good uses:
- last-opened repository id
- last-opened branch name
- graph filter preference
- recent compare target

Bad uses:
- raw blobs
- large snapshots
- commit history

## Local clone model

The client should be able to:
- clone repo metadata + refs
- fetch commit graph incrementally
- fetch only needed blobs/snapshots lazily
- create local commits offline
- push later

## Required local state concepts

- `originRefs`
- `localRefs`
- `workingTree`
- `stagedChanges` should not exist
- `pendingCommits`
- `pendingMergeRequests` optional later
- `dirtyState`
- `lastFetchedUtc`

## Recommended local workflow

1. user opens repo
2. client checks origin status
3. if behind, fetch missing commits/refs
4. user edits locally
5. local working tree becomes dirty
6. user commits locally with message
7. local branch becomes ahead of origin
8. when online, client pushes blobs/snapshots/commit payloads
9. server validates and advances branch tip or returns conflict/divergence

## Push contract recommendation

Client push payload should include:
- repository id
- branch name
- expected remote tip
- missing blobs metadata + upload references
- snapshot manifest
- commit payload
- local commit hash

Server response:
- accepted commit hash
- new server tip
- conflict details if rejected

## Divergence states to model

- `up_to_date`
- `ahead`
- `behind`
- `diverged`
- `missing_local_objects`
- `forbidden`

## Branch checkout rule in WASM

The working tree is attached to one active local branch.

Switching branch must:
- detect dirty changes
- offer commit/discard/cancel
- then load target branch tree

## Merge support in WASM

V1 WASM does not need the full rich notation merge UI yet, but it must be able to:
- fetch compare/merge-preview payloads
- understand conflict hunks
- display mergeable vs conflicting state
- later plug into the notation merge screen without redesign

## Recommended C# service abstractions

- `IRepositoryLocalStore`
- `IRepositorySyncClient`
- `IRepositoryWorkingCopyService`
- `IScoreDiffClient`
- `IRepositoryGraphLayoutService`

## Offline-first truth

The browser can create local history, but the server remains the authoritative origin.
That means every push must be validated server-side before a remote ref moves.
