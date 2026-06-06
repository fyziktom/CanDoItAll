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

## Explicitly forbidden

- `IProcessDriverPack`
- `IProcessDriverRegistry`
- `ProcessDriverRegistry`
- production driver packages
- driver DI registration
- public driver contracts
