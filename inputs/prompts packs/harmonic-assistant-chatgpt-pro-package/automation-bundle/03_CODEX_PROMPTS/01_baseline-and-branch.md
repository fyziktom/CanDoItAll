# 01 — Baseline scan, build, and safety rails

You are Codex operating on the **full repository**. Your goal is to implement the Realtime Harmonic Assistant upgrade described in `/02_DESIGN/*`.

## Step 1 — Baseline understanding (do not change code yet)
1. List and open these files and summarize current behavior in a scratch note (not committed):
   - docs/harmonic-assistant/*
   - src/App.Blazor/Pages/Harmony.razor
   - src/App.Web/wwwroot/harmonicAssistantCanvas.js
   - src/App.Blazor/Services/HarmonicAssistantCanvasInterop.cs
   - src/App.Blazor/Services/HarmonicAssistantSessionService.cs
   - src/App.Blazor/Services/RealtimeChordDetectionSessionService.cs
   - src/MusicTheory.Core/Recognition/RealtimeChordDetectionService.cs
   - src/MusicTheory.Core/Recognition/RealtimeChordWindowDetector.cs
   - src/MusicTheory.Core/Generation/Realtime/RealtimeHarmonicAssistantEngine.cs
   - tests/* (especially chord detection + assistant tests)

2. Run baseline tests:
   - `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~RealtimeChordDetectionTests"`
   - `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~RealtimeHarmonicAssistantTests"`
   - If Playwright tests exist and are runnable, also run:
     - `dotnet test tests/App.Web.PlaywrightTests/Zyphonote.App.PlaywrightTests.csproj`

## Step 2 — Create a work branch + incremental commit policy
- Create a new git branch:
  - `feature/harmonic-assistant-wow-canvas-and-scoring`
- Create a plan for incremental commits:
  1) snapshot DTO v2 + interop plumbing
  2) canvas renderer rewrite (single-line + controls + zoom)
  3) midi scoring tracker + detection integration
  4) scale context inference (including blues/pentatonic)
  5) route planning scoring improvements
  6) tests + diagnostics + polish

## Stop conditions
- If baseline tests fail, stop and fix baseline first (do not proceed with feature work).
- Do not implement multiple major subsystems in a single commit.

## Output of this prompt
- No code changes required yet.
- A short note in your own scratchpad is enough.
