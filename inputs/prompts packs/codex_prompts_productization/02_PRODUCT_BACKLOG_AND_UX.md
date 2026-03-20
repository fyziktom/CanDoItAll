# 02_PRODUCT_BACKLOG_AND_UX.md

## Goal
Define the “north star” product, feature packaging (free/no-account, free account, premium), and implement a first-pass UX reorganization in the Blazor WASM app to make it feel like a coherent product.

## Output docs (create/overwrite)
- `docs/product/product-vision-and-packaging.md`
- `docs/product/ux-information-architecture.md`
- `docs/product/backlog.md`

## Existing UI to analyze and build on (DO NOT ignore)
- Navigation + layout: `src/App.Blazor/Layouts/MainLayout.razor`
- Pages:
  - `src/App.Blazor/Pages/Home.razor`
  - `src/App.Blazor/Pages/Practice.razor`
  - `src/App.Blazor/Pages/Progress.razor`
  - `src/App.Blazor/Pages/Settings.razor`
  - `src/App.Blazor/Pages/ProgressionGenerator.razor`
  - `src/App.Blazor/Pages/LeadSheetView.razor`
  - `src/App.Blazor/Pages/ChordExplorer.razor`
  - `src/App.Blazor/Pages/NotationEditorPlayground.razor`
- Practice/session logic:
  - `src/App.Blazor/Services/PracticeSessionService.cs`
  - `src/App.Blazor/Services/PracticeProgressService.cs`
  - `src/App.Blazor/Services/StatsService.cs`
  - `src/App.Blazor/Services/SettingsService.cs`
- Storage abstraction (needs upgrade to IndexedDB later):
  - `src/App.Blazor/Storage/IAppStorage.cs`
  - `src/App.Blazor/Storage/WasmLocalStorageStorage.cs`

## Product positioning (write into docs)
North-star: **A realtime practice companion for keyboard players**:
- Learn sight-reading with guided curriculum + drills.
- Practice with MIDI input + immediate feedback.
- Understand harmony in realtime (chord detection + harmonic assistant).
- Create and save scores (notation editor) and practice with accompaniment (drums/metronome).
- Works offline; syncs when logged in.

## Feature packaging (MUST be explicit)

### 1) WOW free, no account
Must be usable instantly, offline, without sign-up:
- Quick Start: “Start a 2-minute drill” (sight-reading)
- MIDI chord detector (basic) + chord name list
- Progression generator (basic presets) + open in editor
- Notation editor playground with demo score + export (JSON/MusicXML)
- Local stats + streak (stored locally)

### 2) Free account (limits)
- Sync: basic stats + limited number of saved scores (e.g., 10)
- Access published score library (browse + favorites)
- Basic lesson curriculum tracking across devices

### 3) Premium
- Unlimited saved scores + unlimited sync
- Advanced harmonic assistant modes + style packs + “song form” planning
- Drum generator (advanced patterns + adaptive tempo)
- Advanced analytics (mistake heatmap, tempo graphs)
- OMR import (if enabled) as premium (optional)

### Future: credits/tokens
- Add entitlements model now (do not integrate chain). Prepare API and DB entities.

## UX reorganization (implement in code)
Replace the current “dev-tool style” navigation with user-oriented IA.

### New top-level routes (propose and implement)
- `/` → Dashboard (replaces current Home)
- `/onboarding` → First-run wizard (goal selection + MIDI setup)
- `/lessons` → Curriculum map
- `/practice` → Practice runner (re-uses existing, but driven by lessons)
- `/library` → Score library (local + published + favorites)
- `/editor` → Notation editor (open/create/import)
- `/harmony` → Harmonic assistant (realtime)
- `/settings` → Settings
- `/account` → Login/register/manage (optional)

Keep advanced tools under `/lab/...`:
- `/lab/progression-generator`
- `/lab/chord-explorer`
- `/lab/lead-sheet`
- `/lab/notation-playground`

### Implementation steps (Blazor)
1) Update navigation:
   - Modify `src/App.Blazor/Layouts/MainLayout.razor` to reflect the new IA.
   - Add `data-testid` attributes (or stable IDs) to main nav items for Playwright.
2) Dashboard:
   - Replace `src/App.Blazor/Pages/Home.razor` content with a real dashboard:
     - “Continue lesson”
     - “Quick drill”
     - “Open recent score”
     - “Connect MIDI” status
3) Add onboarding route and basic wizard scaffolding:
   - Create `src/App.Blazor/Pages/Onboarding.razor`
   - Store onboarding state locally (later sync)
4) Add placeholder pages for `/lessons`, `/library`, `/harmony`, `/account`:
   - Initially show “coming soon” but include structure, layout, and test IDs.
   - These pages will be fully implemented in later prompts.

## Backlog output (docs/product/backlog.md)
Create a structured backlog with:
- Epics → Features → User stories → Acceptance criteria
- Priorities: MVP / v1 / v1.5 / v2
- Risks and mitigations

Mandatory epics:
1) Offline-first foundation (IndexedDB, caching, resilience)
2) Accounts + JWT auth + entitlements
3) Sync engine + conflict resolution
4) Lessons curriculum + progress tracking
5) MIDI chord detector
6) Realtime harmonic assistant (canvas)
7) Drum generator (audio scheduling, tempo tracking)
8) Score library (published + user scores + ratings)
9) Admin + moderation + audit logging
10) Testing + CI + observability

## Acceptance criteria
- The app navigation and routes match the new IA.
- Dashboard provides clear calls to action and feels “product-like”.
- Onboarding page exists and persists user choices locally.
- Backlog doc exists with epics/stories/acceptance criteria and clear MVP.
- All new UI elements required for tests include stable selectors (data-testid).

## Verification steps
- Run the app and confirm all routes render.
- Confirm nav works on desktop and mobile widths.
- Confirm onboarding selections persist after refresh.
