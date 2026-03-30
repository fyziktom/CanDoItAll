# Target Solution

## Desired End State

- CanvasLib owns its runtime and static asset surface.
- `ComponentKit` no longer publishes the same CanvasLib JS and CSS files as a second active static-web-asset source.
- CanvasLib `wwwroot` uses folder depth to express responsibility instead of forcing maintainers through giant monoliths.
- Generated public outputs stay declarative and ordered through the manifest and include components.

## Target Folder Shape

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js-src\runtime\workbench\shared\**`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js-src\runtime\workbench\state\**`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js-src\runtime\workbench\render\**`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js-src\runtime\workbench\interaction\**`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js-src\runtime\workbench\overlays\**`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js-src\runtime\workbench\export\**`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js-src\runtime\workbench\runtime\**`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js-src\calendar\runtime\**`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js-src\calendar\render\**`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js-src\calendar\interaction\**`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\css-src\workbench\shell\**`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\css-src\workbench\stage\**`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\css-src\workbench\tooling\**`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\css-src\workbench\overlays\**`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\css-src\workbench\responsive\**`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\workbench\**`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\calendar\**`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\css\workbench\**`

## Architecture Rules

- Keep the build pipeline simple. The manifest should enumerate the ordered source and output files instead of introducing a new bundler.
- Preserve clear asset load order. Shared service files must load before workbench runtime entry files that consume them.
- Prefer small single-purpose source fragments. The generated public list can be longer, but each generated file must still stay under the user’s 2000-line hard cap.
- Keep public asset ownership explicit in the include components. Do not scatter hard-coded asset URLs around the app.
- Treat `ComponentKit` as legacy only. If it must keep code, it should not also masquerade as the current CanvasLib static asset publisher.
