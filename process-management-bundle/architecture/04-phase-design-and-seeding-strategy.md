# Phase Design And Seeding Strategy

## Why Phase 00 Exists

- The original architect bundle started directly at module implementation.
- The repaired bundle adds a pre-implementation foundation phase because current repo evidence shows real duplication risk between CanDoItAll and AgentFramework.
- Phase 00 locks source-of-truth decisions and dev/test seed strategy before product code is touched.

## Post-Phase Repair Rule

- Each implementation phase ends with a generated `post-implementation-bundle-phaseXX`.
- The generated bundle must contain repair subbundles for:
  architecture integrity, canonical model integrity, helper and large-class refactor needs, component-first UI compliance, persistence and seed-data quality, and cross-repo convergence drift.
- The next implementation phase may not start until that generated repair bundle passes its own readiness gate and its repair subbundles are closed or honestly blocked.

## Development And Test Seed Strategy

- Seed at workspace, CRM-HR, project, process-definition, process-run, and artifact/evidence layers.
- Reuse current app services and helpers where possible:
  `ProjectsService`, `IProjectWorkbenchSeedService`, `IManagedArtifactStore`, and existing profile seed helpers.
- Treat seed packs as named scenarios instead of random factory data:
  the same scenario must support integration tests, Playwright flows, demo readiness, and post-phase regression.
- Separate seed layers:
  foundational identities and providers,
  staffing templates and role requirements,
  process definitions and versions,
  run-time evidence and conformance deviations,
  optional IPFS evidence references when the storage seam is ready.

## UI Validation Emphasis

- Authoring, runtime, governance, and management pages must be validated first at large-screen desktop width.
- Canvas and overlay work must verify clipping, lateral overflow, layering, and visual density.
- Every future UI subbundle must name the shared components it intends to use and the routes or screenshots required for proof.
