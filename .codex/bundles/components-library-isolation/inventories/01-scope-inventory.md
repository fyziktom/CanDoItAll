# Scope Inventory

| Area | Current Source | Target Source | Notes |
| --- | --- | --- | --- |
| `CanDoItAll.Components.BaseLib` | `repo://src/CanDoItAll.Components.BaseLib` | `C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.BaseLib` | Move and package. |
| `CanDoItAll.Components.CanvasLib` | `repo://src/CanDoItAll.Components.CanvasLib` | `C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.CanvasLib` | Move and package. |
| `CanDoItAll.Components.Common` | `repo://src/CanDoItAll.Components.Common` | `C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.Common` | Move and package. |
| `CanDoItAll.Components.Charts` | `repo://src/CanDoItAll.Components.Charts` | `C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.Charts` | Move and package. |
| `CanDoItAll.Components.Mermaid` | `repo://src/CanDoItAll.Components.Mermaid` | `C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.Mermaid` | Move and package. |
| `CanDoItAll.Components.OverlayLib` | `repo://src/CanDoItAll.Components.OverlayLib` | `C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.OverlayLib` | Move and package. |
| `CanDoItAll.Components.WebGlLib` | `repo://src/CanDoItAll.Components.WebGlLib` | `C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.WebGlLib` | Move and package. |
| `CanDoItAll.Components.Sandbox` | `repo://src/CanDoItAll.Components.Sandbox` | `C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.Sandbox` | Move and package/build sandbox. |
| `CanDoItAll.Components` | `repo://src/CanDoItAll.Components` | unchanged | Remains in main repo; use packages for moved dependencies. |
| `CanDoItAll.Components.WebGlSandbox` | `repo://src/CanDoItAll.Components.WebGlSandbox` | unchanged | Remains in main repo; use packages for moved dependencies. |
| Space3D projects | `repo://src/Space3D` | `repo://CanDoItAll.Space3D.slnx` | Remain source, removed from main slnx. |
| Component packages | none | `repo://ExternalPackages` | Private package source for main repo. |
| Component Tailwind | `repo://Tailwind` mixed | `C:/repositories/CanDoItAll.Components/Tailwind` | Owns BaseLib output. |
| Main Tailwind | `repo://Tailwind` mixed | `repo://Tailwind` | Owns CanDoItAll-specific output. |
