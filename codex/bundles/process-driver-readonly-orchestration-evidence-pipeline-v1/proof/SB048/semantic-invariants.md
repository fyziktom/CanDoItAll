# SB048 Semantic Invariants

## Invariant SB048-RELEASE-CANDIDATE-SMOKE-MATRIX-IS-BROAD
- Invariant ID: `SB048-RELEASE-CANDIDATE-SMOKE-MATRIX-IS-BROAD`
- Source raw note: `Stable Process Core with domain drivers`
- Expected behavior: Release-candidate proof includes solution build, full unit, focused driver unit, focused process adapter integration, package/reference scans, and source/dependency scans.
- Disallowed shallow implementation: Build-only validation, full-unit-only validation, or omitting focused integration proof after multi-domain process orchestration changes.
- Failing-first test: No deliberate production failure was produced; the first source scan failed on an anti-stub false-positive and was corrected to preserve a meaningful audit.
- Passing test: bundle://proof/SB048/transcripts/build-release-candidate.txt, bundle://proof/SB048/transcripts/full-unit-p16.txt, bundle://proof/SB048/transcripts/focused-p16-driver-unit-matrix.txt, and bundle://proof/SB048/transcripts/focused-p16-process-adapter-integration-matrix.txt
- Changed source files: none in P16; proof/status files only.
- Production assertions: Full unit reports 1129 passed / 0 skipped, driver unit matrix reports 101 passed / 0 skipped, and process adapter integration matrix reports 13 passed / 0 skipped.
- Red-team negative case: Source/dependency scans reject runtime hooks, Core reverse dependency, side-effect APIs, stale docs, stubs, and UI/media drift.
- Downstream dependency check: SB049 red-team trap validation can start from a complete smoke matrix, not a single green build.

## Invariant SB048-DEPENDENCY-GRAPH-STAYS-RUNTIME-FREE
- Invariant ID: `SB048-DEPENDENCY-GRAPH-STAYS-RUNTIME-FREE`
- Source raw note: `Stable Process Core with domain drivers`
- Expected behavior: Driver packages remain package-light/runtime-free, Gateway depends explicitly on known read-only driver packages, Processes consumes the explicit gateway, and Core has no driver reverse dependency.
- Disallowed shallow implementation: Relying on README claims without scanning project references and source namespaces.
- Failing-first test: No deliberate dependency failure was produced; the corrected scan fails if driver packages gain package references, Core references driver packages, or runtime hook tokens appear in the scoped pipeline.
- Passing test: bundle://proof/SB048/transcripts/p16-package-and-reference-scans.txt and bundle://proof/SB048/transcripts/p16-source-and-dependency-scans-fixed.txt
- Changed source files: none in P16; proof/status files only.
- Production assertions: Driver packages show no direct `PackageReference`; Gateway references all current read-only driver packages; Processes references Gateway; Core references Contracts only.
- Red-team negative case: Runtime host/registry/selector/DI/manager/scheduler/workflow hook tokens remain absent from the scoped read-only pipeline.
- Downstream dependency check: Final validation can cite an explicit dependency graph proof before completed validator closure.
