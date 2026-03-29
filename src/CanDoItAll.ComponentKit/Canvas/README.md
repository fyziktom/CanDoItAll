# ComponentKit Canvas Status

`CanDoItAll.ComponentKit.Canvas` is intentionally isolated legacy code.

Current state:
- The active runtime and module consumers reference `CanDoItAll.Components.CanvasLib`, not this project.
- `CanDoItAll.ComponentKit` still carries its own canvas namespace for internal compatibility and catalog surfaces.
- Do not add new runtime consumers here.

Decision:
- Keep `CanvasLib` as the canonical shared canvas path.
- Treat this tree as temporary compatibility surface until a future migration has explicit consumer demand and proof.
