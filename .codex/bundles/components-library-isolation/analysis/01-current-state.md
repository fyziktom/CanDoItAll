# Current State

## Repository Observations

- The main worktree is clean at preparation time.
- `C:/repositories/CanDoItAll.Components` exists and contains only `.git`, `.gitignore`, and `README.md` at preparation time.
- `repo://CanDoItAll.slnx` currently includes all eight projects that must move, plus `CanDoItAll.Components`, `CanDoItAll.Components.WebGlSandbox`, and three Space3D projects.
- The moved component projects currently use project references among themselves:
  - `BaseLib` -> `Common`
  - `OverlayLib` -> `BaseLib`
  - `CanvasLib` -> `BaseLib`, `Common`, `OverlayLib`
  - `WebGlLib` -> `OverlayLib`
  - `Sandbox` -> `BaseLib`, `CanvasLib`, `Charts`, `Common`, `Mermaid`
- Main-repo app/module/test projects project-reference moved component projects directly. These must become package references.
- `repo://Tailwind/package.json` currently builds one output into `repo://src/CanDoItAll.Components.BaseLib/wwwroot/css/output.css`.
- Tailwind contains both component-library styles and main app-specific styles. Main-specific classes observed include `cda-shell-*`, `cda-admin-*`, `cda-reconnect-*`, and `cda-tunable-boundary*`.

## Existing Behavior

- `CanDoItAll.Web` loads BaseLib static assets through `_content/CanDoItAll.Components.BaseLib/css/output.css` and `material-icons.css`.
- Component sandboxes load BaseLib output from the same static web asset path.
- Manager/Tailwind tooling assumes the main repo Tailwind output is `src/CanDoItAll.Components.BaseLib/wwwroot/css/output.css`.

## Impacted Files Or Areas

- `repo://src/CanDoItAll.Components.*`
- `repo://src/CanDoItAll.Web/Components/App.razor`
- Project files under `repo://src`, `repo://tests`, and Space3D that reference moved components.
- `repo://CanDoItAll.slnx` and new Space3D slnx.
- `repo://Tailwind`, `C:/repositories/CanDoItAll.Components/Tailwind`, root `package.json`, and manager Tailwind defaults/tests.
- Documentation under `repo://README.md`, `repo://docs`, `repo://Tailwind/README.md`, and `C:/repositories/CanDoItAll.Components/README.md`.
