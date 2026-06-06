# Hard constraints

- Behavior-preserving refactor only.
- Module-local only: remain under `CanDoItAll.Modules.Processes`.
- No `CanDoItAll.Processes.Core` project, namespace, package, or references.
- No `IProcessDriverPack`, `IProcessDriverRegistry`, `ProcessDriverRegistry`, `IProcessHelperDriver`, or production driver package.
- No UI/Razor/CSS/JS/TS changes.
- No small, medium, mobile, phone, tablet, or responsive proof artifacts.
- Projection source-family order must remain: execution artifacts, process mock, workspace-written, existing managed, response text, provider-native browser, completed decision.
- No coordinator may hide EF writes, file writes, storage placement, or `RecordArtifactAsync` behind a helper that appears pure.
- Any remaining broad host surface must be explicitly justified and tracked for removal.
