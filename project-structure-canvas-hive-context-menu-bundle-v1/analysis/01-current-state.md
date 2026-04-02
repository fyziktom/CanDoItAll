# Current State

- The menu tiles are already rendered as hexagons via `clip-path` in `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\css\workbench\overlays\05-overlays-and-composer.css`, but the root-layer composition is still largely radial and leaves visible gaps between items.
- Geometry lives in `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\03-interaction-and-state.js`. The default root layout uses `getRadialOffsets`, while only selected submenus such as progress, marker, and priority use the `compact-hive` path.
- The current `getCompactHiveOffsets` logic already uses axial-style coordinates, but its spacing constants are intentionally loose, so neighboring hexes do not convincingly share edges.
- Context-menu rendering and submenu placement live in `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\04-context-menu-and-composer.js`.
- Node-context action order is defined in `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureActionCatalogAdapter.cs`; quick-create category order is defined in `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureCanvasCatalog.cs`.
- The recently added shortcut metadata and deterministic shortcut assignment already exist in `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureActionShortcuts.cs`, so this layout pass must preserve that interaction layer rather than re-solving it.
- Existing focused component tests already cover shortcut presence and uniqueness, but there is not yet a test that proves a stable first-ring ordering contract for node menus.
