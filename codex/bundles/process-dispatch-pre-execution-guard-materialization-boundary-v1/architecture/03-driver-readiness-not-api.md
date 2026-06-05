# Driver Readiness, Not Driver API

The long-term design includes process helper drivers for software development, .NET, Rust, browser validation, Office, spreadsheet/business analysis, and agent improvement.

This bundle may add documentation-only vocabulary:

- `UpstreamArtifactGap`
- `ArtifactMaterializationIntent`
- `DatabaseRuntimeRequirement`
- `EvidenceGap`
- `RerunForMissingArtifact`
- `DispatchPreExecutionGuard`

This bundle must not add:

- `IProcessDriverPack`
- `IProcessHelperDriver`
- `IProcessSWDevHelperDriver`
- driver registry
- driver package
- production driver adapter

Why: the evidence/guard vocabulary is still being stabilized inside the process module. Production driver APIs should wait until the vocabulary is proven across at least dispatch, artifact validation, and tool validation boundaries.
