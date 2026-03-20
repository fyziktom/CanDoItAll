# 00_STARTER_PROMPT.md

## Mission

You are working inside a .NET 10 Blazor WebAssembly PWA repository that already contains:
- a learning app (practice modes, stats, generators),
- a custom notation editor (canvas-based UI, playback, recording, Web MIDI),
- a music theory core (generation + recognition engines),
- an OCR/OMR microservice (separate ASP.NET Core API).

Your mission is to **productize** the app into a professional offline-first product with a minimal-cost backend:
- **PWA + WASM first (offline-first).**
- Server used only for: accounts, sync, premium entitlements, published score library, aggregated statistics/telemetry, moderation/admin.
- **Monetization-ready**: free (no account) WOW features, free account with limits, premium subscription, and future tokens/credits + blockchain/NFT/IPFS *prepared architecturally but NOT integrated now*.

You must implement changes as autonomously as possible, producing working code + tests + docs.

## Repository map (you MUST confirm these paths in your working tree)

Solution: `MusicSheetReadingLearner.slnx`

Projects:
- `src/App.Web/` – Blazor WebAssembly host (PWA service worker).
- `src/App.Blazor/` – UI pages/components/services (Radzen UI).
- `src/MusicNotation.Editor/` – Razor Class Library: notation editor (canvas), playback, Web MIDI interop.
- `src/MusicTheory.Core/` – theory + generation + recognition + MIDI tracking + score document model.
- `src/OMR.Service/` – standalone OCR/OMR backend API (already has JWT, rate limiting, ProblemDetails patterns).

Important: the solution references `src/App.Shared/App.Shared.csproj` and `src/App.Tests/App.Tests.csproj`, but these folders may be missing in the current snapshot. You must either:
1) create the missing projects (recommended: `App.Shared` is useful for API DTOs/contracts), OR
2) update the solution file to match reality.
Do NOT ignore this inconsistency.

## Hard requirements (DO NOT SKIP)

### Product direction
- Offline-first PWA; low server cost.
- WOW features must work **without registration**.
- Account is optional but unlocks sync + library + premium.

### Backend
- ASP.NET Core API (new project) with:
  - User accounts
  - Stats storage
  - Score storage + metadata + published library
  - Rating/stars, tags, search
  - Admin role + admin endpoints
  - Audit log (at least admin actions)
  - Robust API: validation, logging, error handling, versioning, rate limiting
- DB: EF Core; default PostgreSQL; switchable SQLite or InMemory via config.

### Auth
- JWT access tokens; login integrated into WASM.
- Role-based access control: at least `admin` vs `user`.
- Security matters: safe password hashing, refresh tokens (or secure alternative), rate limiting, audit, secure storage, input validation.

### Offline-first + Sync
- Local browser storage **IndexedDB** (not localStorage) for:
  - stats
  - lesson progress
  - score documents
  - sync queue (outbox)
- Sync when online + logged in:
  - conflict detection + merge strategy
  - retries, backoff, idempotency, batching
  - “last write wins” is acceptable only for trivial entities; for scores use revisions/conflict copies.

### Missing features that MUST be implemented
A) Playwright UI tests in C# (smoke + e2e, CI-ready)
B) Lessons/guided curriculum + progress tracking (local + optional server)
C) MIDI chord detector with multiple interpretations + notation preview
D) Realtime harmonic assistant (killer feature) + canvas UI + mood controls
E) Detailed drum generator + WebAudio scheduling + adaptive tempo
F) Published score library (search/filter/sort/tags/ratings) + accounts + sync + moderation-ready + NFT/IPFS-ready model (no chain integration now)

### Future blockchain/NFT/IPFS (NO integration now)
- Do not integrate any chain.
- You MUST design entities/interfaces so adding chain later is painless:
  - asset listing, ownership, token id, ipfs cid placeholders
  - “custodial wallet” model (secure storage, rotation, recovery) designed now.

### Coding rules
- All **code comments must be in English**.
- Keep changes incremental and always runnable.
- Add tests (unit/integration/e2e) alongside features.

## Work strategy (follow this exact order)

1) Read and understand the existing codebase and feature set.
2) Implement foundational infrastructure:
   - `App.Shared` contracts
   - IndexedDB storage layer in WASM
   - Backend project skeleton, auth, EF, migrations, API conventions
3) Implement score library + accounts + sync (foundation for monetization).
4) Implement lessons system + tracking (built on top of storage/sync).
5) Implement MIDI chord detection UI + engine wiring.
6) Implement realtime harmonic assistant (engine + canvas UI).
7) Implement drum generator (audio scheduling + patterns + adaptive tempo).
8) Add Playwright UI tests.
9) Add CI checks + Definition of Done.

You must complete each step with verification + tests before moving on.

## Local build & run (provide exact commands; update if needed)

- Build: `dotnet build MusicSheetReadingLearner.slnx`
- Unit tests: `dotnet test MusicSheetReadingLearner.slnx`
- Run PWA: `dotnet run --project src/App.Web`
- Run OMR service: `dotnet run --project src/OMR.Service`
- Run new backend API (to be added): `dotnet run --project src/App.Api`

## Deliverables you must create in-repo
Create a new folder `docs/product/` with:
- `competitor-analysis.md`
- `product-vision-and-packaging.md`
- `ux-information-architecture.md`
- `backend-architecture-security.md`
- `sync-design.md`
- `definition-of-done.md`

## Global acceptance criteria
- PWA runs offline and core training features work without account.
- User can create an account, login, sync scores + progress across devices.
- Published score library works (search/filter/sort/tags/ratings).
- MIDI chord detector works with low latency and shows multiple interpretations.
- Harmonic assistant runs in realtime, stable UI, consistent suggestions.
- Drum generator plays sample-based drums accurately and can follow tempo.
- Playwright tests cover: onboarding/lessons, login, editor open, MIDI detection view, library.
- CI runs: build, unit tests, Playwright smoke tests (at minimum).

---

## You will now execute the remaining prompts in this ZIP in order:
1. `01_RESEARCH_AND_COMPETITOR_ANALYSIS.md`
2. `02_PRODUCT_BACKLOG_AND_UX.md`
3. `03_ARCHITECTURE_BACKEND_AND_SECURITY.md`
4. `04_IMPLEMENTATION_PLAN_STEP_BY_STEP.md`
5. `05_PLAYWRIGHT_TESTS_CSHARP.md`
6. `06_MIDI_CHORD_DETECTION.md`
7. `07_REALTIME_HARMONIC_ASSISTANT_CANVAS.md`
8. `08_DRUM_GENERATOR_AUDIO_AND_LIBRARY.md`
9. `09_SCORE_LIBRARY_ACCOUNTS_AND_SYNC.md`
10. `10_CI_CHECKS_AND_DEFINITION_OF_DONE.md`
