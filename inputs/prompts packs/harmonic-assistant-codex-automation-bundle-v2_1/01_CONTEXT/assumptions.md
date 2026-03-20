# Assumptions

- The canvas renderer supports a V2 payload: nodes use `{ xIndex, worldY, color, probability, kind, pathId, isCurrent }`.
- The app already has “mood brightness” and “colorfulness” values that influence mapping/render hints.
- The app uses JS interop to notify .NET about canvas control changes (e.g., `OnCanvasMoodChanged`).
- A subset of controls still exists in Blazor (module toggles, MIDI connect, recording, etc.). v2.1 migrates them into canvas widgets.

If any assumption is false:
- Create minimal compatibility shims rather than redesigning the whole app.
