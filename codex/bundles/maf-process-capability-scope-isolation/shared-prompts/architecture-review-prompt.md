# Architecture Review Prompt

```text
Run a C# architecture review for the maf-process-capability-scope-isolation bundle.

Verify dependency direction, boundary ownership, pattern choices, testability, and semantic adequacy. Block closure if the implementation only moves code between partial files, hides policy behind prompt text, fails open on invalid scope, couples process core to MAF implementation, or leaves development-specific prompt behavior in common MAF.

Use architecture/01-csharp-boundary-map.md, architecture/02-csharp-dependency-direction.md, architecture/03-csharp-pattern-selection-records.md, architecture/04-csharp-testability-plan.md, and reviews/csharp-architecture-gate.md as the review basis.
```
