# 02 — Add semantic canvas snapshot DTO (v2) + interop plumbing (no visual changes yet)

Goal: introduce a **semantic snapshot** that enables responsive layout in JS (single-line flow, mood axis, lanes, auto-zoom), without breaking the current page.

## Files to modify / add
- Add:
  - `src/App.Blazor/Models/HarmonicAssistantCanvasSnapshotV2.cs`
- Modify:
  - `src/App.Blazor/Services/HarmonicAssistantCanvasInterop.cs`
  - `src/App.Web/wwwroot/harmonicAssistantCanvas.js` (minimal compatibility shim only)

## 1) Add DTO v2
Create records:
- `HarmonicCanvasNodeV2`
- `HarmonicCanvasEdgeV2`
- `HarmonicAssistantCanvasSnapshotV2`

Recommended fields (must match `/02_DESIGN/05_data-model-and-layout.md`):
Node:
- `Id` (string)
- `Label` (string)
- `Kind` (string: "history" | "current" | "future")
- `IsCurrent` (bool)
- `XIndex` (int)
- `PathId` (string?)
- `StepIndex` (int?)
- `Probability` (double)
- `WorldY` (double) // 0..1
- `Color` (string) // hex
- `Meta` (object? / dictionary?) — optional; keep serializable

Edge:
- `FromId`, `ToId`
- `Kind` ("history" | "prediction")
- `Probability` (double)

Snapshot:
- `Nodes`, `Edges`, `Caption`
- optional `Layout`/`RenderHints` if needed later

## 2) Update interop to support v2
In `HarmonicAssistantCanvasInterop.cs`:
- Add `RenderV2Async(HarmonicAssistantCanvasSnapshotV2 snapshot, CancellationToken ct = default)`
- Keep existing `RenderAsync(HarmonicAssistantCanvasSnapshot snapshot)` unchanged for now.

## 3) JS compatibility shim
In `harmonicAssistantCanvas.js`:
- Do NOT rewrite rendering yet.
- Update `render(id, payload)` to store the payload and call `drawFrame`.
- Inside `drawFrame`, detect payload shape:
  - if `payload.nodes` items have `x` and `y`, treat as v1.
  - else if they have `xIndex`/`worldY`, do not draw full layout yet; draw a placeholder caption like:
    - "Canvas v2 payload received; renderer upgrade in progress."
This keeps the app stable while you work incrementally.

## Acceptance criteria
- Solution compiles.
- Existing `/harmony` page still renders as before (using v1 snapshot).
- No tests regress.

## Self-check
- Run:
  - `dotnet build`
  - `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~RealtimeHarmonicAssistantTests"`
