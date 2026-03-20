# Improvement Roadmap for ChatGPT Pro

This roadmap is written as a practical engineering backlog that ChatGPT Pro can execute incrementally.

## Priority Model
- P0: correctness/user trust risks
- P1: major quality and capability gains
- P2: UX and explainability depth
- P3: advanced/optional enhancements

## P0: Correctness and Control Gaps

### P0.1 Make "Lock key" actually usable from UI
Problem:
- Current toggle sets only `LockKey` boolean.
- Engine requires `LockedKeyPitchClass` to apply lock.

Evidence:
- UI sets `settings = settings with { LockKey = value }` at `Harmony.razor:239-243`.
- Engine lock branch checks `settings.LockedKeyPitchClass.HasValue` at `RealtimeHarmonicAssistantEngine.cs:202-208`.

Implementation:
1. Add key picker UI (pitch class dropdown + mode dropdown).
2. Bind to `LockedKeyPitchClass` and `LockedMode`.
3. On lock toggle on, if no key selected, default to top current hypothesis key.
4. Display active locked key badge near status.

Acceptance:
- With lock enabled and key selected, suggestions remain in selected key context across incoming chords.

### P0.2 Add MIDI input connect controls on Harmony page
Problem:
- Assistant starts detection but page offers no input connection control.

Implementation:
1. Add input list using `IMidiService.GetPortsAsync()`.
2. Add connect/disconnect controls.
3. Show connected device name in status chip.

Acceptance:
- User can start from `/harmony` and receive live chord events without visiting another page first.

### P0.3 Add `/harmony` Playwright smoke coverage
Problem:
- No e2e test for this page; only `/harmony/chords` has smoke coverage.

Implementation:
1. Add Playwright test that navigates to `/harmony`.
2. Assert page renders, status chip appears, manual chord apply updates predictions.
3. Add tests for reset behavior and key lock UI.

Acceptance:
- CI validates assistant page does not regress structurally.

## P1: Engine Quality and Musical Coherence

### P1.1 Use style pack device weights and constraints in realtime scoring
Problem:
- Realtime engine checks only enabled device groups.
- Style `DeviceWeights` and `Constraints` are mostly ignored.

Implementation:
1. In `GenerateTransitions`, attach device id metadata for non-diatonic candidates.
2. Multiply base scores with style `DeviceWeights`.
3. Apply lightweight rolling counters for constraints (`MaxSecondaryDominantsPer8`, etc.) using history.
4. Penalize or suppress candidates violating constraints.

Acceptance:
- Changing style packs materially changes distribution and density of non-diatonic devices.
- Unit tests verify constraints and weight impact.

### P1.2 Add confidence/stability gating for jitter reduction
Problem:
- Assistant can react to candidate sets even when chord input may be unstable.

Implementation:
1. Include `detection.Snapshot.IsStable` and score spread checks before committing major hypothesis shifts.
2. Use a "hold last stable hypothesis" fallback window.
3. Decay stale suggestions gracefully instead of abrupt jumps.

Acceptance:
- Rapid arpeggio or noisy transitions produce less suggestion flicker.

### P1.3 Improve key-tracking continuity
Problem:
- Key hypothesis transitions can be abrupt and tied heavily to root containment.

Implementation:
1. Track rolling key confidence over N events.
2. Penalize key changes unless evidence crosses threshold.
3. Add optional modulation mode in settings to allow/limit key drift.

Acceptance:
- Longer phrase inputs keep coherent key context unless strong modulation cues exist.

## P2: Explainability and UX

### P2.1 Surface per-step scale hints in UI
Problem:
- Engine computes `SuggestedScale` but UI hides it.

Implementation:
1. Display scale hint per step in suggestion card.
2. Distinguish "functional reason" and "color reason".

Acceptance:
- User can see both why a chord is suggested and what scale to use over it.

### P2.2 Make canvas responsive and interactive
Problem:
- Fixed coordinates can crowd/clip on narrow screens.
- No resize event wiring despite existing interop support.

Implementation:
1. Wire `ResizeObserver` or window resize callback to `CanvasInterop.ResizeAsync`.
2. Replace fixed coordinates with normalized layout by canvas dimensions.
3. Add hover tooltips with reasons and probabilities.
4. Add path highlight on card hover.

Acceptance:
- Canvas remains legible across mobile/desktop widths.

### P2.3 Improve status communication
Problem:
- Status chip class currently reflects "supported" not "connected".

Implementation:
1. Derive status state enum: `Unsupported`, `Uninitialized`, `Disconnected`, `Connected`.
2. Style class by state, not just support flag.
3. Add remediation CTA for unsupported cases.

Acceptance:
- Visual state accurately reflects actual readiness for realtime input.

## P3: Advanced Improvements

### P3.1 User feedback loop for ranking calibration
Implementation:
1. Let user accept/reject suggestions.
2. Log anonymous local feedback events (offline-safe queue).
3. Adjust scoring priors over time (local personalization).

### P3.2 Alternative search strategy experimentation
Implementation:
1. Add A/B mode for Monte Carlo rollout or dynamic programming variant.
2. Compare musical continuity and compute latency.

### P3.3 Phrase-aware harmonic memory
Implementation:
1. Segment history into phrase windows (cadence detection).
2. Bias transitions by phrase position (opening/mid/cadential).

## Concrete Task Queue for ChatGPT Pro

1. Implement key-lock UI + engine integration checks.
2. Add MIDI input connection controls to Harmony page.
3. Add `/harmony` Playwright smoke test and one manual chord interaction test.
4. Add transition metadata and style weight multiplication in realtime engine.
5. Add stability gating in harmonic session update flow.
6. Add scale hint rendering in suggestion cards.
7. Add canvas resize wiring and normalized positioning.
8. Add lightweight telemetry hooks for update latency and candidate churn.

## Suggested Definition of Done Additions
1. Lock-key path tested with at least 2 fixed keys and both major/minor modes.
2. `/harmony` e2e test verifies manual input path and no-crash render.
3. Realtime engine tests cover style weight influence and constraint enforcement.
4. Median update latency measured under 250ms in browser profile run.
