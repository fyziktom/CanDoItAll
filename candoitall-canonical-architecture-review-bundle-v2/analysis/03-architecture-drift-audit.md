
# Architecture drift audit

## Baseline

Baseline used:

- previous repo snapshot: `CanDoItAll-canvas-drawing-refactor`
- previous review bundle: `candoitall-canonical-architecture-review-bundle`

## Growth signals

| Signal | Old | New | Delta | Interpretation |
| --- | --- | --- | --- | --- |
| Projects (.csproj) | 41 | 42 | +1 | New module surface added. |
| C# files | 539 | 599 | +60 | Substantial wave growth. |
| Razor files | 304 | 337 | +33 | UI surface grew materially. |
| `manager` suspicious-name markers | 23 | 42 | +19 | More orchestration-leaning surface area. |
| `god_service` suspicious-name markers | 35 | 35 | +0 | No numeric increase, but existing hotspots remain. |

## Drift categories

| Category | Current state |
| --- | --- |
| Source-of-truth drift | High and accelerating |
| Boundary drift | High and accelerating |
| Projection drift | High and accelerating |
| Policy / auth drift | Medium |
| Runtime / operational drift | Medium |
| Integration drift | High |
| Naming / concept drift | High |
| Testability drift | High |
| Dependency drift | Medium |

## Strongest drift hotspots

| Hotspot | Symptoms | Findings | Why |
| --- | --- | --- | --- |
| ProjectWorkbenchModels.cs | 2931 lines; sync, read, write, projection, media, transfer, metadata, links | ACR-001, ACR-002, ACR-004, ACR-005, ACR-006, ACR-008, ACR-009, ACR-010, ACR-014 | Core hotspot |
| ProjectWorkbenchMetadata.cs | 869 lines; JSON family model, marker normalization, partial validation | ACR-003, ACR-008, ACR-012, ACR-014 | Semantic hotspot |
| ProjectStructurePage.PartyIntegration.cs | 505 lines; UI writes metadata and assignment rows | ACR-012, ACR-013, ACR-015 | Boundary hotspot |
| CrmHrServices.cs | 4704 lines overall; assignment save only checks project/party existence | ACR-013, ACR-015 | Cross-module integrity hotspot |
| ProjectStructureCanvasCatalog.RichDefinitions.cs | UI authoring catalog includes participant/work item semantics | ACR-003, ACR-014 | UI-owned semantics hotspot |
| Resource/Validation/TestLab responsibility fields | Module-local owner/responsible IDs fragment responsibility truth | ACR-012, ACR-015 | Cross-module drift hotspot |

## Audit judgment

Drift is **not random**. It is accumulating around a predictable axis:

- node meaning
- responsibility ownership
- projection vs truth
- cross-module overlays

That is exactly where the CRM/HR wave landed, so the drift is **accelerating in the most important area**, not in an irrelevant edge of the product.

## Acceptable drift for now

The following drift can be tolerated until later phases:

- service decomposition (ACR-009) can wait until truth boundaries stabilize
- lease narrowing (ACR-010) can wait until mutation/invariant rules stop moving
- some storage/artifact cleanup (ACR-007) can follow after node carrier decomposition begins

## Unacceptable drift now

The following drift should **not** be deferred:

- reparent cycle/invariant weakness
- missing invariant tests
- soft node-scoped assignment references
- duplicated node-level actor truth
- unsupported note→task/decision lifecycle

## Audit conclusion

The CRM/HR wave is strategically good, but it raised the cost of architectural hesitation.

If another feature wave lands before the Phase 0–2 stabilizations, the system will become harder to reason about precisely where the product differentiates most: the shared graph of people, agents, and project work.
