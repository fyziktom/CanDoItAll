# Proof Strategy

## Required source scans

- No Process Core project.
- No process-driver production source.
- No MAF/Tooling product dependency.
- No prohibited viewport proof paths.
- No helper stubs.
- No direct file/storage/DbContext usage in helpers unless the helper is explicitly the artifact content reader boundary.
- `ProcessStepTransitionRequest` builder sets all artifact-validation context fields.

## Required tests

- Existing architecture guardrails.
- Finalizer focused integration tests.
- Artifact validation/projection regression slices.
- Manager artifact recovery and post-recovery validation tests.
- Runtime invariant blocking tests.
- Transition request field parity tests.
- Full solution build.

## Large-screen policy

Browser validation is `N/A` unless UI files change. If unexpected UI proof is needed, use large desktop/PC only. Do not create small/medium/mobile proof.
