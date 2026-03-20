# Assumptions and constraints

## Assumptions
- Codex will run on the **full repository** (this documentation pack may not include all shared theory classes).
- Existing classes such as `ChordInstance`, `ChordBuilder`, `ChordLibrary`, `PitchMath`, `ChordRecognitionEngine`, and `ChordVoicingAnalyzer` exist in the repo and compile today.
- The app is Blazor (WASM or WebAssembly-hosted) and uses `Radzen` UI components.
- The `/harmony` page can call JS interop freely and can attach a `ResizeObserver`.

## Constraints
- Must use **HTML5 Canvas** for the “killer feature” visualization.
- Must avoid layout wrapping: render as a single, horizontally flowing timeline.
- Must support long sessions without becoming unreadable.
- Must keep update latency roughly within the existing target (<250ms median from stable chord input).
- Thread-safety matters: MIDI events can arrive asynchronously; services use debouncing and async gates.
- All code comments must be **English**.

## Terminology for the upgrade
- **Mood axis**: a vertical coordinate representing “brighter/happier” (up) vs “darker” (down).
- **Energy**: a separate metric representing vivid/sharp/tension vs calm.
- **WorldY**: absolute mood-axis position assigned to a chord/harmony.
- **ViewY**: screen position after applying a “camera offset” so the current chord remains centered.
- **Branch lane**: a parallel horizontal lane offset (up or down) used to keep multiple suggestion paths readable.
