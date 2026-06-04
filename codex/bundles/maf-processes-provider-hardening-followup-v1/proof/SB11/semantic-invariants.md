# SB11 Semantic Invariants

- Invariant ID: `SB11-INVARIANT-001`
- Source raw note: `RQ-012` Final integration smoke must prove the provider seam and process evidence semantics on the final source shape, not just compile-time structure.
- Expected behavior: The app composes runtime tool providers through DI without requiring every provider to be registered, process automation keeps receipt-required project-structure writeback semantics precise, subprocess artifact projection creates parent-run-scoped evidence with lineage identity, and at least one real process runtime path completes through the integration harness with workspace-backed artifacts.
- Disallowed shallow implementation: Passing a build while subprocess projection reuses child artifact paths, accepting negated `project_structure_asset_create` text as a required writeback, completing baseline process seeds with missing required artifacts, or relying on manual claim text instead of process integration tests.
- Failing-first test: `bundle://proof/SB11/transcripts/adversarial-old-subprocess-projection-scan.txt` records that the old subprocess projection storage-path reuse is absent.
- Passing test: `bundle://proof/SB11/transcripts/dotnet-test-unit-full.txt`, `bundle://proof/SB11/transcripts/dotnet-test-integration-process.txt`, and `bundle://proof/SB11/transcripts/dotnet-build-slnx.txt`.
- Changed source files: `bundle://proof/SB11/source-assertions/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB11/source-assertions/integration-smoke-source-assertions.txt`.
- Red-team negative case: A future change that restores child-path reuse for projected subprocess artifacts fails the adversarial scan, and a future change that weakens process evidence semantics fails the process-filtered integration suite.
- Downstream dependency check: SB12 may start because the final provider composition, receipt/evidence semantics, subprocess projection, and real process runtime smoke pass on the current source shape.
