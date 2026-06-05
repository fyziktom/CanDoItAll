# Driver Readiness, But Not Driver API

This bundle prepares future drivers only by documenting evidence families and stabilizing rule names.

## Future evidence families to map

- `RequiredArtifactSatisfactionEvidence`
- `RecordedArtifactEvidence`
- `FreshImplementationArtifactEvidence`
- `ProviderNativeBrowserEvidence`
- `ResponseTextDeliverableEvidence`
- `ExternalTargetGroundingEvidence`
- `ManagedArtifactPathEvidence`
- `QualityValidationEvidence`
- `DocumentDeliverableEvidence`
- `SpreadsheetDeliverableEvidence`
- `BusinessAnalysisDeliverableEvidence`

## Do not implement

- `IProcessDriverPack`
- `IProcessHelperDriver`
- `IProcessEvidenceDriver`
- driver registry
- production driver descriptors
- driver packages

The future driver API should come after module-local runtime semantics are stable enough to avoid encoding today's dispatcher internals into public contracts.
