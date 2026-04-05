# Plugin-Wave Readiness

## Verdict

**GO with guarded rollout** for the external plugin wave on the current architecture.

## Reasoning

| Dimension | Verdict | Why |
| --- | --- | --- |
| Canonical truth | Pass | System-managed projections are assembled at read time and no longer live as canonical Workbench rows. |
| Universal node stability | Pass | The carrier now owns semantic/scheduling/spatial data while bindings and references hold specialized payload and foreign-owner state. |
| Node-kind extensibility | Pass | `ProjectNodeKindRegistry` centralizes descriptors, visual profiles, subtype mutation, and metadata normalization. |
| Brainstorming → structured lifecycle | Pass | Reclassification now persists lifecycle history with source/target snapshots and explicit transition mode. |
| CRM/HR seam | Partial | The seam is still not globally atomic, but it now has durable mutation records and validated recovery/compensation paths. |
| Plugin platform | Pass | Connector/provider/resource integration is manifest- and registry-driven instead of enum/switch growth. |
| Guardrail testability | Pass | Architecture tests, integration proof, component proof, and Playwright flows now cover the reopened plugin seams. |

## Safe Conclusion

The codebase is now a sound enough base to start the next connector/plugin wave, but rollout should stay incremental:

- add one real plugin at a time behind manifest-driven tests
- keep extending policy/health/schema proof with each new connector
- treat cross-module writes as a seam that must stay durable and observable, not silently best-effort
