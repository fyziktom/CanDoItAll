# Explicit Verification Gateway Design

## Intent
SB019 adds a contract-only gateway design for known read-only verification lanes. The gateway boundary is an allow-list of typed lane descriptors, not a registry, selector, host, DI extension, manager command, scheduler hook, workflow hook, or runtime execution surface.

## Contract Types
- `CanDoItAll.Processes.Drivers.Abstractions.Gateway.ProcessDriverVerificationGatewayLane`
- `CanDoItAll.Processes.Drivers.Abstractions.Gateway.ProcessDriverVerificationGatewayLaneDescriptor`
- `CanDoItAll.Processes.Drivers.Abstractions.Gateway.ProcessDriverVerificationGatewayLaneRules`

## Allowed Lanes
| Lane | Scope | Permission mode | Primary evidence | Core descriptor family | Allowed operations |
| --- | --- | --- | --- | --- | --- |
| `DotNetRustTranscriptVerification` | `DotNetRustTranscriptVerification` | `VerificationOnly` | `CommandTranscript` | `ExecutionEvidence` | `InspectExistingEvidence`, `ReturnDiagnostics`, `ExplainDenial` |
| `RuntimeEvidenceConsistency` | `RuntimeFactsRead` | `ManagerReadonly` | `CoreDescriptor` | `ExecutionEvidence` | `ReadProcessFacts`, `ReturnDiagnostics`, `ExplainDenial` |
| `ArtifactEvidenceConsistency` | `ArtifactEvidenceRead` | `VerificationOnly` | `CoreDescriptor` | `ArtifactProjectionEvidence` | `InspectExistingEvidence`, `ReturnDiagnostics`, `ExplainDenial` |
| `OfficeEvidenceRead` | `OfficeEvidenceRead` | `VerificationOnly` | `OfficeReadonlyArtifact` | N/A | `InspectExistingEvidence`, `ReturnDiagnostics`, `ExplainDenial` |
| `BusinessAnalysisRead` | `BusinessAnalysisRead` | `VerificationOnly` | `BusinessReadonlyArtifact` | N/A | `InspectExistingEvidence`, `ReturnDiagnostics`, `ExplainDenial` |

## Denied Surfaces
- No dynamic lane discovery.
- No registry, selector, host, provider, or runtime pack abstraction.
- No DI registration or service collection extension.
- No manager command, scheduler hook, workflow hook, or endpoint mapping.
- No file, directory, workspace, storage, HTTP, Graph, Office, Gmail, shell, package restore, or external connector access.
- No process mutation, claim mutation, transition, finalizer, retry, provider repair, workspace write, or storage write.

## Versioning
Contract version is `1.10.0` after SB035 added typed artifact-evidence diagnostics for missing lineage, trust/sensitivity mismatches, and satisfaction inconsistencies. SB034 added the artifact-evidence read-scope rule for the pre-existing `ArtifactEvidenceRead` lane. SB032 added typed business-analysis diagnostic categories for missing requirements, unsupported assumptions, contradiction markers, and evidence gaps. SB031 added the business-analysis supplied evidence payload envelope kind/factory and business-analysis read-scope rule. SB028 added the Office supplied evidence payload envelope kind/factory and Office read-scope rule. SB025 normalized audit facts with explicit lane and typed evidence references. SB022 added supplied evidence content envelopes. SB019 added public gateway lane descriptors and the `ArtifactEvidenceRead` scope. The additions are compatible but must remain source-backed by the public API snapshot.

## SB020 Handoff
SB020 may implement explicit constructors or factories for currently implemented lanes only. It must not add runtime discovery, DI registration, generic dispatch by string, manager commands, scheduler hooks, workflow hooks, or execution-capable behavior.
