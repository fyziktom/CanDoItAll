# Driver Readiness Map

This is documentation-only. Do not implement production driver APIs in this bundle.

## Future Driver Candidates

| Future driver area | Evidence/rule families prepared by current refactor | Timing |
| --- | --- | --- |
| Generic process verification helper | route facts, finalizer facts, artifact satisfaction facts | after Core contracts are stable |
| Software development helper | implementation proof, build/test/run evidence, file mutation evidence | after module-local evidence contracts are stabilized |
| DotNet helper | .NET host evidence, dotnet build/test/run receipt classification | after software helper boundary exists |
| Browser proof helper | provider-native browser output, browser evidence path rules | after browser proof rule contracts are stable |
| Business analysis helper | document deliverable evidence, decision artifacts, review summaries | after generic artifact evidence contracts are stable |
| Office/Excel helper | document/spreadsheet read-only verification, generated report validation | after plugin tool governance is stable |

## Do Not Do Now

- Do not create `IProcessDriverPack`.
- Do not create a driver registry.
- Do not create package names such as `CanDoItAll.Processes.DriverPacks.*`.
- Do not bind route handlers or projection coordinators to driver concepts yet.

## Final Driver Decision

Driver work remains documentation-only. The source scan in `bundle://proof/SB027/transcripts/source-scan.txt` verified no production process-driver API tokens in the process module source. The next driver-related work should be another readiness map after Process Core candidate contracts are stable, not production driver code.
