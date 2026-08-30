# Architecture Checkpoints

| Checkpoint | Required evidence | Unlock |
| --- | --- | --- |
| CP-RELAY | SB01/SB02 direct policy and public SDK/imported-graph tests; no network authority widening; .csproj diff | SB05/SB06 |
| CP-CAPTURE | SB03 persisted privacy/outcome tests; no duplicate canonical storage | SB07/capture host proof |
| CP-RETENTION | SB04 referenced/orphan lifecycle proof and quota consistency | SB05/SB07 |
| CP-CONTRACT | SB05 safety/measurement review and SB06 generated schema semantics | Final docs/export |
| CP-MERGE-FROZEN | Current CodeAnalytics + actual project graph; independent proof review; one named broad gate and separately required host/migration lanes | Manual merge recommendation only |

For every relevant checkpoint run csharp-architecture-review-gate: responsibility, dependency direction, construction, testability and extension seam. No new runtime partial. If any extraction is added, prove the moved responsibility left the old owner, before/after size or thin-facade behavior, direct tests without original runtime and a negative shallow-delegation test. Since no extraction is required here, do not create one only to manufacture shrink metrics.
