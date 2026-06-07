# Projection Validation Evidence Inventory

## Scope
- Covers SB013 projection source order, projection lineage, provider-native browser evidence, and validation satisfaction facts.
- Separates immutable descriptors from projection orchestration, file copy/write, managed path resolution, validation probing, and artifact persistence.
- Feeds SB014 implementation and SB015 parity proof.

## Pure Core Descriptor Fields
- Projection lineage facts: source kind, source execution run id, recovery execution run id, recovered-for execution run id, projected execution run id, workflow run id, workflow artifact id, subprocess run id, source artifact id, rework packet id, source external reference key, content hash, projection identity hash, runtime-source flag, record-only-source flag, recovery-lineage flag, source-artifact flag, and provider-native browser evidence flag.
- Projection source order facts: source kind, producer kind, projection order, runtime-source flag, record-only-source flag, record-only precedence flag, and provider-native browser evidence flag.
- Provider-native browser facts: browser evidence kind, normalized tool name, declared-path presence, matched-output presence, and required-artifact satisfaction eligibility.

## Module-Owned Runtime Fields
- Projection coordinator ordering, filesystem copying, file reads, file writes, storage classification, managed path resolution, and artifact record persistence.
- Provider-native browser output discovery, browser output path matching, working-directory checks, safe path resolution, file existence checks, and file length checks.
- Artifact expectation matching, current candidate mutation state, dispatch claim checks, transition mutation, and validation orchestration.
- Process driver proposal/registry/selector concepts, which remain out of production scope for this bundle.

## Adapter Ownership
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionEvidenceDescriptorAdapter.cs` is the projection evidence bridge.
- The adapter maps module lineage/source/provider-native browser facts into Core descriptors.
- `ProcessArtifactProjectionLineageBuilder`, `ProcessArtifactProjectionOrchestrator`, and `ProcessProviderNativeBrowserOutputFacts` use the adapter without directly importing Core.

## Validation
- Source assertions: `bundle://proof/SB015/transcripts/source-assertions.txt`.
- Behavioral proof: `bundle://proof/SB015/transcripts/projection-validation-focused-integration-tests.txt`.
- Boundary scan: `bundle://proof/SB015/transcripts/dispatch-core-reference-scan.txt`.
- Gate semantics: `bundle://proof/SB015/semantic-invariants.md`.
