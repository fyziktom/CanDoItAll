# Driver Readiness Map

This is documentation only. Do not implement driver APIs.

Gate C update: artifact validation rules are now grouped behind process-module-local helpers, but no driver contracts, Process Core APIs, or driver packs were introduced.

| Future driver capability | Validation semantics needed | Current bundle contribution |
| --- | --- | --- |
| Generic manager verification | Read-only validation of evidence completeness | Rule family names and proof categories |
| SW development helper | build/test/browser proof satisfaction | Quality validation rule extraction |
| DotNet helper | dotnet build/test/run evidence | Quality validation naming and tests |
| Rust helper | cargo build/test/clippy evidence | Same quality proof abstraction labels |
| Business analysis helper | document/table deliverable satisfaction | Artifact title/content/path matching rules |
| Office/Excel helper | spreadsheet artifact validation | Path/content/sensitivity/trust rules |
| Browser/Web helper | screenshot/network/console evidence | Provider-native visual rule extraction |

## Current Helper Boundary

- `ProcessArtifactValidationSnapshot` and `ProcessArtifactValidationSnapshotBuilder`: dispatcher expectation snapshot seam only.
- `ProcessArtifactPathValidationRules`: managed path and declared path matching rules.
- `ProcessArtifactTextMatchRules`: title, slug, content-signal, visual-token, and narrative-purpose matching rules.
- `ProcessArtifactProviderNativeVisualValidationRules`: provider-native browser path/tool classification and visual scoring.
- `ProcessArtifactQualityValidationRules`: build warning, zero-test, browser-proof text, quality contract, and placeholder request rules.
- `ProcessArtifactProjectStructureRequirementValidationRules`: project-structure downgrade/defer/drop preservation rules.

These helpers are intentionally local to `CanDoItAll.Modules.Processes`; they are not public driver APIs.
