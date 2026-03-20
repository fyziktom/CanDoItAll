# Harmonic Assistant Test and Observability Plan

## 1) Current Coverage Snapshot

### Unit tests present
1. `tests/MusicTheory.Tests/RealtimeHarmonicAssistantTests.cs`
- Repeated chord stability behavior.
- Style pack influence on reasons.
- Determinism of top beam path.

2. `tests/MusicTheory.Tests/RealtimeChordDetectionTests.cs`
- Chord window stability.
- Sustain downweighting effect.
- Known-input ranking.

### E2E coverage present
- `tests/App.Web.PlaywrightTests/ProductFlowsTests.cs` includes `/harmony/chords` smoke.
- No direct `/harmony` page e2e test currently.

## 2) Baseline Verification Run
Command executed:
```powershell
dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~RealtimeHarmonicAssistantTests"
```

Observed outcome:
- Passed: 3
- Failed: 0
- Skipped: 0

## 3) Critical Missing Tests

### 3.1 Engine correctness tests
1. Lock-key behavior test
- Ensure `LockKey + LockedKeyPitchClass` forces key context.

2. Style weight effect test
- After style-weight integration, verify weighted devices reorder candidates.

3. Constraint enforcement test
- Ensure max secondary dominants/substitutions are capped over rolling window.

4. Stability gate test
- Noisy/unstable snapshots should not produce high-churn suggestion jumps.

### 3.2 Session/service tests
1. Debounce coalescing test in `HarmonicAssistantSessionService`.
2. Manual chord parser tests (invalid symbols, lowercase/uppercase, accidentals, slash chords if added).
3. Reset semantics test (history/hypothesis reset + settings retention behavior).

### 3.3 UI/component tests
1. `/harmony` page render with no MIDI support.
2. Manual chord apply updates cards and canvas snapshot.
3. Lock key UI controls update settings as expected.
4. Resize behavior if canvas normalization is implemented.

### 3.4 E2E tests
1. Navigate to `/harmony` and assert all sections render.
2. Apply manual chord (`Cmaj7`) and assert suggestions appear.
3. Reset and assert history clears.
4. Lock key and verify display state.

## 4) Observability Gaps

Current gaps:
- No structured client-side metrics for:
  - detection-to-suggestion latency,
  - hypothesis churn,
  - suggestion path stability,
  - manual-input usage rate.

- No explicit debug mode to inspect:
  - weighted detection candidates,
  - active hypotheses before/after update,
  - transition candidate pool per beam depth.

## 5) Recommended Metrics

### 5.1 Performance
1. `harmonic.update.total_ms`
- From detection event timestamp to UI update completion.

2. `harmonic.engine.ms`
- Engine `Update` runtime only.

3. `harmonic.canvas.render_ms`
- JS render function duration.

### 5.2 Quality/Stability
1. `harmonic.top_path_flip_rate`
- Frequency of top-path identity changes across consecutive updates.

2. `harmonic.key_context_flip_rate`
- Frequency of key display changes.

3. `harmonic.empty_suggestion_rate`
- Percentage of updates yielding no suggestions.

### 5.3 Usability
1. `harmonic.manual_input_count`
2. `harmonic.midi_connected_session_ratio`
3. `harmonic.lock_key_enabled_ratio`

## 6) Instrumentation Plan (Minimal Intrusion)
1. Add optional diagnostics interface:
- `IHarmonicAssistantDiagnostics`
- no-op implementation by default

2. Add metric hooks in:
- `HarmonicAssistantSessionService.QueueUpdateAsync`
- `RealtimeHarmonicAssistantEngine.Update`
- `Harmony.razor` update handler around render invocation

3. Add debug panel (behind feature flag) with:
- latest weighted candidates,
- hypotheses,
- top transitions per depth.

## 7) Performance Guardrails
1. Keep total update median below 250ms.
2. Keep p95 below 350ms on reference hardware/browser.
3. Avoid increasing combined debounce above current 205ms unless compensated by smarter event gating.

## 8) Suggested CI Additions
1. Add unit test category for realtime assistant.
2. Add Playwright test tag for `/harmony`.
3. Optional perf benchmark test (non-blocking initially) to trend update times.
