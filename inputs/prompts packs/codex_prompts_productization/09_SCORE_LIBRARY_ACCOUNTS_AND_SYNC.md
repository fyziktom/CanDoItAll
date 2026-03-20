# 09_SCORE_LIBRARY_ACCOUNTS_AND_SYNC.md

## Goal
Implement the score library + accounts + offline-first sync end-to-end:
- local library (IndexedDB)
- optional login to enable sync
- published library (server-backed)
- tags/search/sort
- ratings
- moderation-ready pipeline
- future NFT/IPFS-ready models (no chain integration now)

## Existing code to leverage
- Notation document serialization:
  - `src/MusicTheory.Core/NotationEditor/Formats/NotationJsonFormatService.cs`
  - `src/MusicTheory.Core/NotationEditor/Formats/MusicXmlSubsetService.cs`
- Notation editor UI surface:
  - `src/App.Blazor/Components/NotationEditor.razor`
- Storage abstraction (needs expansion):
  - `src/App.Blazor/Storage/*`
- OMR backend patterns for security & rate limiting:
  - `src/OMR.Service/Program.cs`

## Part 1 — Local score library (IndexedDB)
### A) Local schema
In IndexedDB create stores:
- `scores` (key: scoreId)
- `scoreVersions` (key: versionId, index: scoreId)
- `syncOutbox` (key: opId, index: entityId)
- `userState` (tokens, lastSyncCursor, entitlements)
- `lessonProgress` (for later)
- `stats` (for later)

### B) C# services
Create in `src/App.Blazor/Services/`:
- `LocalScoreRepository`
  - CRUD metadata
  - save/load current `ScoreDocument`
  - versioning for local changes
- `ScoreAutosaveService`
  - debounced save from editor events
- `LibrarySearchService`
  - in-memory index built from local metadata for fast search

### C) UI
Create:
- `src/App.Blazor/Pages/Library.razor` route `/library`
- `src/App.Blazor/Pages/Editor.razor` route `/editor`
Library features:
- My Scores (local)
- Published (server)
- Favorites (local list)
- Search bar, tag filter, sort dropdown (recent, rating, title)
- Score cards: title, composer, tags, last edited, sync status
- Actions: open, duplicate, delete, publish request

Editor features:
- open selected score document
- local save indicator
- export/import (JSON + MusicXML subset)
- “Publish” button (requires login)

Add test IDs:
- `library-search`, `library-score-card-*`, `library-open`, `editor-save`, `editor-status`

## Part 2 — Backend library + publishing + ratings
### A) Publishing flow
Implement statuses:
- Draft (private)
- PendingReview (requested publish)
- Published
- Rejected (with reason)
Rules:
- Only owner can request publish.
- Admin can approve/reject.
- Published scores are searchable.

### B) Search & filtering
Implement server-side query:
- `q` full-text (title/composer/tags)
- `tags` list
- `sort` (recent, rating, downloads/favorites later)
- paging: `page`, `pageSize`

### C) Ratings
- Auth required to rate.
- One rating per user per score (upsert).
- Provide aggregates: average + count.

### D) “NFT-ready” but no chain
Extend metadata model (nullable):
- `AssetListingId`, `TokenId`, `Chain`, `ContractAddress`, `IpfsCid`
Expose read-only fields in library endpoints.

## Part 3 — Sync engine (offline-first)
### A) Outbox
When offline or not logged in:
- write changes to local stores
- enqueue outbox operation:
  - UpsertScoreMetadata
  - UpsertScoreDocument
  - DeleteScore
Operations must be idempotent:
- include `opId` GUID and `clientTimeUtc`

### B) Push/Pull protocol
Use cursor-based sync:
- Client stores `lastSyncCursor` per user.
- Pull: server returns changed entities since cursor.
- Push: client sends list of ops; server returns applied + conflicts.

### C) Conflict policy
Scores are documents; merge is hard. Strategy:
- If local and server both changed from same base version:
  - create a new local version called “Conflict copy (deviceName/time)”
  - keep server version as “Remote”
  - show UI banner letting user pick which to keep/publish
Stats/progress: merge by additive counters and de-duplicated attempt IDs.

### D) Retry policy
- exponential backoff with jitter
- cap retries
- show non-blocking UI status

## Security considerations
- Only allow access to “my scores” for the authenticated user.
- Published library is public read, but write actions require auth.
- Rate limit: login, register, publish requests, ratings.

## Tests (MUST)
- Unit tests:
  - local repository CRUD (serialize/deserialize ScoreDocument)
  - outbox idempotency behavior
- API integration tests:
  - create score, add version, publish request, rate
- Playwright e2e:
  - create local score → shows in library
  - login → sync starts → score appears with “synced” badge

## Acceptance criteria
- User can create scores offline and reopen them.
- After login, scores sync to server and are available after fresh login.
- Published library can be searched and sorted; ratings work.
- Conflict scenario produces a visible conflict state and preserves both versions.

## Verification steps
- Manual:
  - create a score offline, refresh, confirm present
  - go online and login, confirm sync completes
  - request publish, confirm it appears in admin moderation list (admin role)
- Automated:
  - `dotnet test` (unit + integration)
  - Playwright e2e passes
