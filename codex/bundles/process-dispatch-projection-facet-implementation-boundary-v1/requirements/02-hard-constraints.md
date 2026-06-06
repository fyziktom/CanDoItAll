# Hard Constraints

- No `CanDoItAll.Processes.Core`.
- No production process driver abstractions, including but not limited to:
  - `IProcessDriverPack`
  - `IProcessDriverRegistry`
  - `ProcessDriverRegistry`
  - `CanDoItAll.Processes.DriverPacks`
  - `IProcessHelperDriver`
- No public process contracts movement.
- No EF entity movement.
- No DB migration.
- No UI/Razor/CSS/JS/TS changes.
- No small-screen, medium-screen, mobile, phone, tablet, or responsive proof artifacts.
- No source-family order change.
- No behavior change; this is architecture refactoring only.
- No stub, TODO, NotImplemented, fixture-specific, template-only, or fake placeholder implementation.
