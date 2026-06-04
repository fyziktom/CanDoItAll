# Next Phase Strategy

## Recommended Sequence

1. Clean merge surface and rerun entry proof.
2. Add provider descriptors/metadata without changing behavior.
3. Refactor MAF provider-composition code so it is provider-neutral in names and structure.
4. Migrate project-structure and image-generation hard-coded attach paths into registered providers.
5. Pause for a forced refactor checkpoint.
6. Split the Processes-owned provider into maintainable files/classes.
7. Add purpose/access groundwork for manager read-only verification.
8. Add provider observability and receipt tagging.
9. Update docs and architecture guards.
10. Run integration smoke and final red-team.

## Explicit Cutline

This bundle ends before any `CanDoItAll.Processes.Contracts` or `CanDoItAll.Processes.Core` extraction. The next bundle after this one may prepare contracts/core extraction only if SB12 closes cleanly.
