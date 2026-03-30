# Target Solution

## Target State

- CanvasLib keeps one canonical static-asset tree in source control for the active publish surface.
- Asset generation tooling remains responsible only for generated include components or validation, not for mirroring identical JS/CSS files into a second committed tree.
- CanvasLib C# code mirrors the logical runtime surface:
  - `Components\Calendar\**`
  - `Components\Workbench\**`
  - `Components\Graph\{Interaction,Overlays,Primitives,State}\**`
  - `Canvas\Graph\{Interaction,Overlays,Primitives,State}\**`
  - `Canvas\Workbench\**` for workbench contracts and adjacent models

## Design Rules

- Prefer moving existing files into coherent folders over introducing new abstractions.
- Keep namespaces stable unless a consumer-compatible namespace file or project-level `_Imports.razor` already guarantees stability.
- Preserve the current asset URLs under `_content/CanDoItAll.Components.CanvasLib/...`.
- Split data contracts by responsibility, not by arbitrary file size chunks.

## Duplicate Strategy

- The bundle treats duplicate code in two tiers:
  - active duplicate: must be removed or consolidated in this bundle
  - legacy isolated duplicate: may be retired in this bundle if it is proven unused
- `CanDoItAll.ComponentKit` is planned as a retirement candidate because it duplicates the canvas surface but is not part of the active solution graph.

## Validation Boundaries

- The bundle does not redesign CanvasLib behavior.
- The bundle does not tackle unrelated large-file hotspots outside CanvasLib unless a required compile or runtime dependency forces a nearby edit.
