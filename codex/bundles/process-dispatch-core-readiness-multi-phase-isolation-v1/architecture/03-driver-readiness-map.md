# Driver readiness map: documentation only

No production driver API may be added in this bundle.

## Future driver seams prepared by this bundle

| Future driver concern | Prepared by | Notes |
| --- | --- | --- |
| Software development route support | Route decision and finalizer boundaries | Still documentation-only |
| .NET runtime validation | Evidence/finalizer/projection existing helpers | Do not introduce DotNet driver yet |
| Rust support | Future evidence/tool requirement intent model | Do not implement now |
| Office/business-analysis processes | Candidate, artifact, finalizer intent vocabulary | Do not implement now |
| Browser proof driver | Projection/browser evidence helper boundaries | Still module-local |
| Subprocess delegation driver | Subprocess runtime/projection service | Still module-local |

## Execution decision

No production driver API is ready to implement after this bundle. The safe next step is to let the new module-local services age under tests, then define public contracts for pure route/lifecycle decisions only. EF-backed hydration, claim lifecycle, artifact projection, and AgentFramework execution remain application/infrastructure concerns and should not be moved into a Core or driver package.

## Explicitly forbidden

- `IProcessDriverPack`
- `IProcessDriverRegistry`
- `ProcessDriverRegistry`
- production driver packages
- driver DI registration
- public driver contracts
