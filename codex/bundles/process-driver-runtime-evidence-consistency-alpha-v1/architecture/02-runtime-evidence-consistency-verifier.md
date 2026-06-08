# Runtime Evidence Consistency Verifier Architecture

## Purpose
Create a second verification-only domain driver alpha that checks consistency across already-produced Core descriptor payloads.

## Candidate inputs
- `ProcessExecutionEvidenceDescriptor`
- `ProcessFinalizerEvidenceDescriptor`
- `ProcessRetryDiagnosticDescriptor`
- `ProcessNoProgressRetryDiagnosticDescriptor`
- `ProcessProviderRepairDiagnosticDescriptor`
- `ProcessArtifactProjectionEvidenceDescriptor`
- `ProcessArtifactValidationRequirementDescriptor`
- Evidence references and transcript/proof references from driver abstractions.

## Candidate diagnostics
- Execution succeeded but retry descriptor says retry.
- Finalizer result applies transition while finalizer result descriptor says no result.
- Retry primary failure kind contradicts missing/failed tool facts.
- Provider repair descriptor claims repair outcome without provider failure.
- No-progress descriptor has signal but missing fingerprint/evidence reference.
- Projection validation descriptor claims provider-native evidence but source order omits provider-native source.
- Evidence hashes missing or duplicate evidence references disagree.
- Descriptor family missing for a requested lane.

## Output requirements
- immutable response,
- diagnostics with severity/category/message/evidence reference,
- audit facts for accepted and denied operations,
- redaction descriptor,
- no-mutation proof,
- contract version.

## Hard denials
The verifier cannot load descriptors from disk, query database, call process services, update artifacts, trigger repair, apply transitions, or schedule retries.
