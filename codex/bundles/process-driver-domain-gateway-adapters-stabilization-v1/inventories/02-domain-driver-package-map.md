# Domain Driver Package Map

| Package | Current role | Next action |
| --- | --- | --- |
| `CanDoItAll.Processes.Drivers.TranscriptVerification` | Implemented verification-only alpha | Keep stable; no runtime host. |
| `CanDoItAll.Processes.Drivers.RuntimeEvidence` | Implemented verification-only alpha | Keep stable; no runtime host. |
| `CanDoItAll.Processes.Drivers.ArtifactEvidence` | Implemented package-level alpha | Add gateway method and process adapter. |
| `CanDoItAll.Processes.Drivers.OfficeEvidence` | Implemented package-level alpha | Add gateway method and process adapter; deny Graph. |
| `CanDoItAll.Processes.Drivers.BusinessAnalysis` | Implemented package-level alpha | Add gateway method and process adapter; deny CRM/business mutation. |
| `CanDoItAll.Processes.Drivers.ObservationAggregation` | Implemented read-only response aggregator | Add process adapter; no persistence. |
| `CanDoItAll.Processes.Drivers.VerificationGateway` | Explicit transcript/runtime gateway | Expand explicit lanes only; no generic dispatch. |
