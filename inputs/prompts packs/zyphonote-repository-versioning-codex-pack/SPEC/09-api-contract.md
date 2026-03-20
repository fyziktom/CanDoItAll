# API contract

## API design goals

The repository API must serve both:

- PHP web UI
- future Blazor WASM client

That means:
- clean DTOs,
- branch/ref compare-and-swap behavior,
- batch fetch support,
- explicit conflict payloads.

## High-level endpoint groups

### Repository discovery
- `GET /api/v1/repos/mine`
- `GET /api/v1/repos/{repoId}`
- `GET /api/v1/repos/{repoId}/graph`

### Object fetch
- `GET /api/v1/repos/{repoId}/commits/{commitHash}`
- `GET /api/v1/repos/{repoId}/snapshots/{snapshotHash}`
- `GET /api/v1/repos/{repoId}/blobs/{blobHash}`
- `POST /api/v1/repos/{repoId}/blobs/batch-get`

### Commit/push
- `POST /api/v1/repos/{repoId}/commit`
- `POST /api/v1/repos/{repoId}/push`

### Pull/origin status
- `POST /api/v1/repos/status-batch`
- `POST /api/v1/repos/pull-batch`

### Branches/refs
- `POST /api/v1/repos/{repoId}/branches`
- `POST /api/v1/repos/{repoId}/refs/{refName}/move`
- `DELETE /api/v1/repos/{repoId}/branches/{branchName}`

### Compare / merge preview
- `POST /api/v1/repos/{repoId}/compare`
- `POST /api/v1/repos/{repoId}/merge-preview`
- `POST /api/v1/repos/{repoId}/merge`

### Forks
- `POST /api/v1/repos/{repoId}/fork`

### Merge requests
- `POST /api/v1/merge-requests`
- `GET /api/v1/merge-requests/{mergeRequestId}`
- `GET /api/v1/merge-requests/mine`
- `POST /api/v1/merge-requests/{mergeRequestId}/merge`
- `POST /api/v1/merge-requests/{mergeRequestId}/close`

## Commit endpoint rule

Because there is no staging area, `POST /commit` should treat the submitted working tree as the full commit content.

Client sends:
- target branch
- expected current tip
- commit message
- changed files and/or full snapshot file map
- optional base commit

Server:
- validates permissions
- persists missing blobs
- builds snapshot
- creates commit
- moves branch tip with CAS

## Compare endpoint rule

The compare endpoint must be generic enough for:
- text/meta diffs
- playlist/package structured diffs
- score semantic diffs

## Merge preview endpoint rule

Input:
- base commit
- ours commit
- theirs commit
- target branch optional

Output:
- mergeable state
- merged snapshot hash if clean
- diff summary
- conflict hunks if not clean

## Status batch endpoint rule

The WASM app should be able to ask:
- what repos changed on origin,
- ahead/behind versus local branch tips,
- which refs should be fetched.

Input:
- local repo summaries

Output:
- server branch tips
- ahead/behind/diverged states
- changed repo list

## Permissions

### Read
- owner
- admins
- public/fork-allowed readers depending on visibility

### Write
- owner
- future collaborators
- admins for moderation/repair only

### Merge
- target repo owner/admin
- future maintainers

## DTO rule

Include:
- `repositoryId`
- `entityType`
- `rootEntityId`
- `defaultBranch`
- `forkPolicy`
- `visibility`
- `branchHeads`
- `currentCommitHash`
- `aheadBehind`

Do not hide commit hashes behind only legacy version ids.
