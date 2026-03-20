# 04 — Build semantic canvas snapshot v2 from engine output (no renderer rewrite yet)

Goal: generate the v2 graph model (nodes/edges with xIndex, worldY, colors) using real history + suggestions.

## Files to modify
- `src/App.Blazor/Pages/Harmony.razor`
- (optional) `src/App.Blazor/Models/HarmonicAssistantCanvasSnapshotV2.cs` if fields need tweaks

## 1) Add configurable history step-count (UI)
Add a new setting to `AssistantSettings` (in `src/MusicTheory.Core/Generation/Realtime/RealtimeHarmonicAssistantEngine.cs` record definition):
- `int HistorySteps = 32` (default; clamp 8..256)

Update `Harmony.razor` UI:
- Add a slider or dropdown for `HistorySteps`.
- When changed, call `HarmonicAssistantSession.ConfigureAsync(settings)`.

Note: This setting will later be used by the engine for retention AND by the canvas for rendering.

## 2) Create `BuildCanvasSnapshotV2(update)` method
In `Harmony.razor` code-behind:
- Implement `BuildCanvasSnapshotV2(HarmonicAssistantUpdate assistantUpdate)` that returns `HarmonicAssistantCanvasSnapshotV2`.

### Node rules
- History nodes:
  - take last `settings.HistorySteps` from `assistantUpdate.State.History`
  - assign xIndex: `-(count-1) .. 0` with current at 0
  - kind: "history" for non-last, "current" for last
  - probability: use top candidate probability (clamped)
  - chord: use `eventItem.Candidates.First()` (best)
  - compute `WorldY` and `Color` using `HarmonyVisualMapping.Compute(chord)`
- Future nodes:
  - for each suggestion path (take top 3 or 4):
    - pathId = e.g. `p{pathIndex}`
    - for each step:
      - xIndex = stepIndex (1..H)
      - kind = "future"
      - probability = path.Probability (or step-relative, but keep it simple)
      - compute WorldY/Color using mapping based on chord name:
        - Preferred: if chord parsing from `step.ChordName` is possible, parse to `ChordInstance`.
        - If not feasible, use a fallback heuristic based on chord name string.
        - (Codex: find existing chord parse utilities in repo; use them if present.)
- Edges:
  - history edges connect consecutive history nodes
  - prediction edges connect:
    - current -> first future step
    - step -> next step within a path
  - include edge probability

### Caption
- Keep a short status:
  - stable detection? top probability? etc.

## 3) Wire snapshot v2 into render pipeline (feature flag)
Add a boolean field in `Harmony.razor`:
- `private bool useCanvasV2 = false;` (keep false until JS renderer rewrite is complete)

In `RenderCanvasAsync()`:
- if useCanvasV2: call `CanvasInterop.RenderV2Async(snapshotV2)`
- else: keep the existing v1 call

## Acceptance criteria
- Build succeeds.
- Existing behavior unchanged (still using v1 renderer).
- Settings include HistorySteps and UI can change it without errors.

## Self-check
- `dotnet build`
- `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj`
