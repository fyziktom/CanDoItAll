# Appendix for `candoitall-bundle-execution`

Add this section to the execution skill.

## C# Architecture Execution Addendum

When executing an architecture-relevant C# subbundle:

1. Read the C# architecture files before editing code.
2. Run the subbundle entry gate and confirm the architecture checkpoint prerequisites.
3. Do not add a partial class unless the subbundle explicitly allows it under the partial-class policy.
4. If a new project reference is needed but not planned, repair the bundle before adding it.
5. If a cyclic reference appears, stop and extract a smaller contract instead of adding the reference.
6. If tests require constructing the old runtime for extracted behavior, keep the subbundle open and improve the seam.
7. After implementation, update:
   - `reviews/csharp-architecture-gate.md`
   - `architecture/02-csharp-dependency-direction.md`
   - `architecture/03-csharp-pattern-selection-records.md` if pattern choice changed
   - `architecture/04-csharp-testability-plan.md` with actual tests
8. Run `csharp-architecture-review-gate` before closure.
9. Record source assertions proving:
   - old class shrank or became a thin facade
   - moved behavior lives in the new owner
   - project references point correctly
   - tests instantiate extracted behavior directly
10. Do not start dependent feature subbundles until the architecture checkpoint passes.
