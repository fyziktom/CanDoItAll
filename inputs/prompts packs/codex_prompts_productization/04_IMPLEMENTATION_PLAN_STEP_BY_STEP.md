# 04_IMPLEMENTATION_PLAN_STEP_BY_STEP.md

## Goal
Provide a highly actionable, check-box style implementation plan with checkpoints so the work can run autonomously for many hours.

## How to work
- Make small, testable commits (even if you cannot actually commit in this environment, follow the discipline).
- After each step: build + run relevant tests.
- Keep the app runnable at all times.
- Add `data-testid` to any UI element that is interacted with in tests.
- All code comments must be in English.

---

## Phase 0 — Repo hygiene + baseline
### Tasks
- [ ] Confirm solution integrity (`MusicSheetReadingLearner.slnx` references missing projects).
- [ ] Create missing projects (`src/App.Shared`, `src/App.Api`, `src/App.Api.Tests`, `tests/App.Web.PlaywrightTests`, etc.) OR fix solution references.
- [ ] Add a `docs/product/` folder and commit baseline docs placeholders.

### Verification
- [ ] `dotnet build MusicSheetReadingLearner.slnx`
- [ ] `dotnet test MusicSheetReadingLearner.slnx`

---

## Phase 1 — Shared contracts + app settings
### Tasks
- [ ] Create `src/App.Shared` with DTOs:
  - `Auth*Dto` (register/login/refresh/me)
  - `ScoreDto`, `ScoreVersionDto`, `ScoreDocumentDto`
  - `LibraryQueryDto`, `LibraryResultDto`
  - `SyncPushDto`, `SyncPullDto`, `SyncConflictDto`
  - `EntitlementDto`
- [ ] Add JSON serialization options (camelCase, enums as strings) in both API and WASM.
- [ ] Add `ApiOptions` configuration in WASM (`src/App.Blazor/Services/ApiOptions.cs`).

### Verification
- [ ] Build compiles and DTOs used from both ends.

---

## Phase 2 — Backend skeleton (auth + EF + Swagger + rate limiting)
### Tasks
- [ ] Implement `src/App.Api`:
  - EF Core `AppDbContext` + provider switching (Postgres/SQLite/InMemory)
  - JWT auth + refresh token rotation
  - Roles + policies
  - Rate limiting + ProblemDetails + correlation IDs
- [ ] Implement minimal score CRUD endpoints (metadata + document storage stub).
- [ ] Add `src/App.Api.Tests` integration tests.

### Verification
- [ ] All API tests pass.
- [ ] Swagger shows endpoints; login works.

---

## Phase 3 — WASM login + token handling + entitlement gating
### Tasks
- [ ] Add `/account` page:
  - login + register
  - “continue as guest”
- [ ] Add `AuthClient` service in `src/App.Blazor/Services/`:
  - stores access token in memory
  - refresh token flow (cookie or storage-based) per backend decision
- [ ] Add an `EntitlementsService` that:
  - caches plan info locally
  - gates premium UI routes/features
- [ ] Add a simple “Premium badge” UI and upsell panel (non-invasive).

### Verification
- [ ] Login works; user sees account state; premium gating works.

---

## Phase 4 — IndexedDB local database (foundation for offline-first)
### Tasks
- [ ] Introduce IndexedDB wrapper:
  - JS: `src/App.Web/wwwroot/indexedDbInterop.js` (or RCL if shared)
  - C#: `src/App.Blazor/Storage/IndexedDbStorage.cs`
- [ ] Migrate stats/progress from localStorage to IndexedDB:
  - `StatsService`, `PracticeProgressService`, onboarding state
- [ ] Add migration strategy (versioned stores; best-effort migration of old localStorage keys).

### Verification
- [ ] Offline reload preserves data.
- [ ] No regressions in Practice/Progress pages.

---

## Phase 5 — Score library (local) + editor integration
### Tasks
- [ ] Implement local score library:
  - list, search, tags, sort, open in editor
  - store ScoreDocument JSON in IndexedDB
- [ ] Add `/library` and `/editor` routes:
  - create new score from templates
  - open recent score
  - autosave + manual save
- [ ] Ensure `MusicTheory.Core/NotationEditor/Formats/NotationJsonFormatService.cs` is used.

### Verification
- [ ] User can create/save/open/edit scores offline.

---

## Phase 6 — Sync engine (client + server)
### Tasks
- [ ] Implement outbox queue (IndexedDB):
  - operations: upsert score metadata, upsert score document, delete
  - idempotency keys
- [ ] Implement API sync endpoints (`/api/v1/sync/pull`, `/push`).
- [ ] Conflict strategy:
  - for scores: create “conflict copy” (new version) and let user choose
  - for stats: merge additive counters, de-dupe attempts by ID
- [ ] Add “Sync status” UI on dashboard and in library.

### Verification
- [ ] Create score offline → go online → sync to server → open on another device (simulated).
- [ ] Conflict scenario produces deterministic, user-resolvable outcome.

---

## Phase 7 — Lessons & guided curriculum
### Tasks
- [ ] Implement lesson catalog (JSON definitions) + runner UI:
  - `/lessons` map
  - `/practice` runs lesson-driven exercises
  - progress saved locally + synced
- [ ] Extend `PracticeSessionService` to accept “lesson constraints”.

### Verification
- [ ] User sees “current step”, can skip, can pick lesson.
- [ ] Progress persists across refresh and (if logged in) syncs.

---

## Phase 8 — MIDI chord detector
### Tasks
- [ ] Create chord detection service (debounce + chord window).
- [ ] Use existing `MusicTheory.Core/Recognition/ChordRecognitionEngine.cs`.
- [ ] Display candidates with:
  - name, intervals, inversion
  - missing/extra notes
  - notation preview and “send to editor”
- [ ] Add `/harmony` or `/midi-chords` view.

### Verification
- [ ] Latency and stability targets met (see prompt 06).

---

## Phase 9 — Realtime harmonic assistant (killer feature)
### Tasks
- [ ] Implement engine with history + probabilistic hypotheses.
- [ ] UI: canvas visualization + mood controls (“brighter/darker”, “verse/chorus”).
- [ ] Make suggestions musically plausible (voice-leading + functional harmony + selected style pack).

### Verification
- [ ] Meets acceptance criteria (see prompt 07).

---

## Phase 10 — Drum generator
### Tasks
- [ ] Pattern editor + kit selection
- [ ] WebAudio scheduling (look-ahead)
- [ ] Adaptive tempo from MIDI input
- [ ] Bundle a small legal sample kit OR provide built-in synthesized fallback.

### Verification
- [ ] Metronomic accuracy + tempo following targets (see prompt 08).

---

## Phase 11 — Playwright tests + CI
### Tasks
- [ ] Add C# Playwright tests (smoke + e2e) for:
  - onboarding/lesson flow
  - login
  - open editor and verify load
  - MIDI detection view renders
  - library browse
- [ ] Add GitHub Actions workflow:
  - build, unit tests, API tests, Playwright smoke
- [ ] Add Definition of Done doc + quality gates.

### Verification
- [ ] CI green.
