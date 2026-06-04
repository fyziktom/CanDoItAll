# Hard Constraints

- No `CanDoItAll.Processes.Core` project.
- No process driver-pack project.
- No EF entity movement.
- No UI/Razor/CSS/JS changes unless explicitly required by a failing test unrelated to this bundle; if so, stop and record a scope exception.
- No mobile/small/medium proof artifacts.
- No public tool name changes.
- No artifact external reference key format drift without an explicit migration note and test.
- No duplicate-projection weakening.
- No required-artifact satisfaction weakening.
