# Dispatcher Artifact Hotspots

## Projection Hotspots

- Execution artifacts from process automation run details.
- Process mock artifacts from serialized session state.
- Workspace-written artifacts inferred from receipts.
- Existing managed artifact files.
- Response text artifacts.
- Provider-native browser artifacts.
- Decision artifacts created for completed steps.

## Validation Hotspots

- Missing concrete proof summary.
- Browser/runtime failure detection.
- Quality validation evidence, build warnings, zero-test success.
- Project-structure requirement downgrade detection.
- Required artifact expectation satisfaction.
- Placeholder-only, stale, wrong-producer, hash mismatch, unavailable evidence.
- Current-run-only implementation/browser proof.

## Next Boundary Shape

Start with pure planners/classifiers:

- `ProcessArtifactExpectationMatcher`
- `ProcessArtifactProjectionLineageBuilder`
- `ProcessArtifactProjectionCandidateFactory`
- `ProcessArtifactEvidenceValidationRules`

Only after tests prove parity should DB/storage mutation be moved behind a service.
