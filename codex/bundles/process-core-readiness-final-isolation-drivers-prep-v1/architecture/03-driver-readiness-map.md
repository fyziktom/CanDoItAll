# Driver Readiness Map

This is documentation-only. Do not implement production driver APIs in this bundle.

## Future driver candidates

| Future driver area | Evidence/rule families prepared by current refactor | Timing |
| --- | --- | --- |
| Generic process verification helper | route facts, finalizer facts, artifact satisfaction facts | after Core contracts are stable |
| Software development helper | implementation proof, build/test/run evidence, file mutation evidence | after module-local evidence contracts are stabilized |
| DotNet helper | .NET host evidence, dotnet build/test/run receipt classification | after SW dev helper boundary exists |
| Browser proof helper | provider-native browser output, browser evidence path rules | after browser proof rule contracts are stable |
| Business analysis helper | document deliverable evidence, decision artifacts, review summaries | after generic artifact evidence contracts are stable |
| Office/Excel helper | document/spreadsheet read-only verification, generated report validation | after plugin tool governance is stable |

## Do not do now

- Do not create `IProcessDriverPack`.
- Do not create a driver registry.
- Do not create package names such as `CanDoItAll.Processes.DriverPacks.*`.
- Do not bind route handlers or projection coordinators to driver concepts yet.
