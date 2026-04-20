# Target solution

## Recommended architecture

### Universal layer

Add a new Razor class library:

- `src/CanDoItAll.Components.WebGlLib`

Responsibilities:

- host component,
- typed generic scene contracts,
- JS runtime bootstrapping,
- diagnostics and automation DTOs,
- asset loading,
- DOM mirror/accessibility layer.

### Runtime layer

Inside the library, JavaScript owns:

- renderer and scene graph,
- camera,
- hit-testing,
- drag preview,
- connection preview,
- image export,
- automation snapshots.

### Adapter layer

Outside the universal library, add a process-specific adapter that:

- loads real template-backed `ProcessDefinitionEditorModel` data,
- reuses current process IDs and categories,
- projects those semantics into the generic WebGL scene contract,
- adds deterministic center-lane and depth rules.

### Host layer

Add a dedicated concept host:

- `src/CanDoItAll.Components.WebGlSandbox`

Responsibilities:

- template switching,
- camera/view presets,
- in-memory scene edits,
- reset/reload,
- screenshot proof route.

## Visual strategy

- default perspective camera in the sandbox,
- deterministic center-lane 3D layout with role spread and semantic depth,
- HTML/DOM labels,
- semantic color/style mapping by current process connection categories,
- screenshot-friendly layout and deterministic test mode.

## Why this shape is preferred

It gives the concept a real executable path while preserving:

- universal library boundaries,
- production route isolation,
- meaningful proof surfaces,
- future optional reuse by other modules.
