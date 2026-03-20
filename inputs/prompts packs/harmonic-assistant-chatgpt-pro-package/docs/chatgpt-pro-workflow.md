# ChatGPT Pro Workflow for Improving Harmonic Assistant

## Goal
Provide a repeatable, high-signal workflow so ChatGPT Pro can improve the Realtime Harmonic Assistant with minimal re-discovery time.

## 1) Working Principles
1. Always preserve deterministic behavior unless a change explicitly targets controlled randomness.
2. Treat musical coherence and UI trust as first-class quality dimensions.
3. Keep low-latency requirement in scope (<250ms target from stable chord to updated suggestions).
4. Prefer incremental PRs with focused tests over one large rewrite.

## 2) Startup Context Checklist
When starting a new ChatGPT Pro session, ingest these first:
1. `docs/harmonic-assistant/realtime-harmonic-assistant-deep-dive.md`
2. `docs/harmonic-assistant/algorithm-and-scoring-notes.md`
3. `docs/harmonic-assistant/harmonic-assistant-source-map.md`
4. `docs/harmonic-assistant/improvement-roadmap-for-chatgpt-pro.md`
5. `docs/harmonic-assistant/test-and-observability-plan.md`

Then open core code:
1. `src/App.Blazor/Pages/Harmony.razor`
2. `src/App.Blazor/Services/HarmonicAssistantSessionService.cs`
3. `src/App.Blazor/Services/RealtimeChordDetectionSessionService.cs`
4. `src/MusicTheory.Core/Generation/Realtime/RealtimeHarmonicAssistantEngine.cs`
5. `src/App.Web/wwwroot/harmonicAssistantCanvas.js`

## 3) Recommended Implementation Order

### Step 1: Lock key functional path
1. Add locked key + mode controls in UI.
2. Persist those into `AssistantSettings`.
3. Add tests proving lock branch is used.

### Step 2: MIDI input controls on Harmony page
1. Add input list + connect/disconnect.
2. Improve status chip state mapping.
3. Add e2e smoke validation for this path.

### Step 3: Style-aware transition scoring
1. Add transition metadata for device type.
2. Apply style pack weight multipliers.
3. Add constraints counters and tests.

### Step 4: Stability and explainability
1. Add unstable-detection gating.
2. Surface `SuggestedScale` in cards.
3. Add optional diagnostics output for hypotheses/transitions.

### Step 5: Canvas responsiveness
1. Wire resize path.
2. Normalize node layout for viewport size.
3. Optional hover/interaction layer.

## 4) Prompt Templates for ChatGPT Pro

### Template A: Focused code change
```text
Improve the Realtime Harmonic Assistant by implementing [feature].
Constraints:
- Preserve deterministic suggestions.
- Keep update latency budget in mind.
- Add/adjust unit tests.
- Modify only files in the harmonic assistant source map unless required.
Before coding, summarize current behavior from these files:
- [list files]
Then implement and show a concise diff rationale.
```

### Template B: Bug diagnosis
```text
Diagnose why [symptom] happens in Harmony page.
Inspect:
- Harmony.razor
- HarmonicAssistantSessionService.cs
- RealtimeHarmonicAssistantEngine.cs
- RealtimeChordDetectionSessionService.cs
Return:
1) root cause with line references,
2) minimum safe fix,
3) regression tests to add.
```

### Template C: Algorithm tuning
```text
Tune suggestion ranking to [objective] without adding ML.
Use only deterministic rules and produce:
1) updated scoring formula,
2) threshold rationale,
3) test cases proving improvement and no regressions.
```

## 5) Guardrail Questions Before Merging Changes
1. Did we add or preserve deterministic behavior where expected?
2. Did we unintentionally increase update delay?
3. Are reasons shown to users still accurate and specific?
4. Can a first-time user operate from `/harmony` without visiting other pages?
5. Are there tests for both happy path and edge cases introduced?

## 6) Anti-Patterns to Avoid
1. Silent exception swallowing without diagnostics in new code paths.
2. Expanding heuristics without tests that capture the intended musical behavior.
3. Adding UI controls that do not wire fully into `AssistantSettings`.
4. Mixing large refactors with feature work in one PR.
5. Introducing non-deterministic randomness into beam ranking.

## 7) Suggested PR Sizing
1. PR-1: key lock UI + tests.
2. PR-2: MIDI connect controls + status state + e2e smoke.
3. PR-3: style-weight integration + constraint checks + engine tests.
4. PR-4: stability gating + scale hints + docs.
5. PR-5: canvas responsive layout + resize wiring + visual tests.

## 8) Validation Commands
Use these as baseline:
```powershell
dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~RealtimeHarmonicAssistantTests"
dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~RealtimeChordDetectionTests"
```

Add Playwright validation when `/harmony` e2e is added:
```powershell
dotnet test tests/App.Web.PlaywrightTests/Zyphonote.App.PlaywrightTests.csproj --filter "FullyQualifiedName~Harmony"
```
