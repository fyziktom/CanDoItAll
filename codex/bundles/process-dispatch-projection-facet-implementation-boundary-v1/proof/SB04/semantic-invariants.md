# SB04 Semantic Invariants

- Invariant ID: SB04_INV_001
- Source raw note: Continue smaller dispatcher isolation steps; do not rush Process Core; preserve original projection behavior; keep UI proof out of scope.
- Expected behavior: The projection refactor keeps the existing projection flow while replacing the single all-facet implementation with focused module-local facet classes.
- Disallowed shallow implementation: Keeping one class that implements every projection facet, injecting the dispatcher into focused facets, touching UI files, adding Process Core or process driver APIs, reordering projection source families, or leaving stub code.
- Failing-first test: N/A - process-only guardrail refactor without an intentional behavior delta; adversarial source assertions cover the rejected shallow cases in bundle://proof/shared/transcripts/source-assertions.txt.
- Passing test: bundle://proof/shared/transcripts/unit-projection-tests.txt and bundle://proof/shared/transcripts/integration-projection-tests.txt.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionFacetImplementations.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionFacets.cs, repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs.
- Production assertions: bundle://proof/shared/transcripts/source-scan-no-core-driver-ui.txt, bundle://proof/shared/transcripts/source-scan-no-all-facet.txt, bundle://proof/shared/transcripts/source-scan-coordinator-boundaries.txt, and bundle://proof/shared/transcripts/source-scan-source-family-order.txt.
- Red-team negative case: bundle://proof/shared/transcripts/source-scan-no-all-facet.txt rejects broad-host, all-facet, and dispatcher-injection tokens in production source.
- Downstream dependency check: bundle://proof/shared/transcripts/full-build.txt and bundle://proof/shared/transcripts/source-assertions.txt close the downstream projection gates without Process Core, driver, UI, source-order, or stub drift.
