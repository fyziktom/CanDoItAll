# Readiness Assessment

Process Core extraction is still premature.

The branch is ready for another module-local seam because candidate hydration and candidate construction are now more stable. Subprocess runtime/projection should be isolated next because it remains mostly self-contained and currently mixes planning, side effects, and finalizer orchestration in `Dispatch.cs`.

Driver work should stay documentation-only. The right near-term driver preparation is vocabulary mapping:

- `DelegatedProcessEvidence`
- `SubprocessRunOutcomeEvidence`
- `SubprocessArtifactProjectionEvidence`
- `CapabilityGapEvidence`
- `ChildProcessArtifactSource`
- `ParentScopedArtifactProjection`

These should be documented but not implemented as production APIs.
