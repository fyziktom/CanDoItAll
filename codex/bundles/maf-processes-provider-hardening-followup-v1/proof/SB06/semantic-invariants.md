# SB06 Semantic Invariants

- Invariant ID: `SB06-INVARIANT-001`
- Source raw note: `RQ-007` The provider seam must be reviewed after product migrations so neither MAF nor Tooling becomes a second product-tool monolith.
- Expected behavior: Tooling remains product-neutral; MAF provider composition uses provider-neutral names; MAF has no undocumented product module references; only Security and Workspace module references are allowed and documented; first-party product tool providers live in owning modules.
- Disallowed shallow implementation: Passing builds while leaving MAF product attach helpers, allowing Tooling to reference product modules, or leaving direct MAF Processes/Projects/Workbench references undocumented.
- Failing-first test: Static architecture tests now encode the forbidden references and allowed-list, so future regressions fail before runtime behavior is exercised.
- Passing test: `bundle://proof/SB06/transcripts/static-architecture-tests.txt`, `bundle://proof/SB06/transcripts/forbidden-namespace-scans.txt`, `bundle://proof/SB06/transcripts/tooling-build.txt`, and `bundle://proof/SB06/transcripts/maf-build.txt`.
- Changed source files: `bundle://proof/SB06/source-assertions/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB06/source-assertions/provider-boundary-source-assertions.txt`.
- Red-team negative case: The static tests and forbidden scans would fail direct MAF Processes/Projects/Workbench references, old project/image attach methods, process-specific wrapper names, or Tooling references to MAF/product modules.
- Downstream dependency check: SB07 may start because MAF/Tooling boundaries are clean and the remaining large provider is explicitly the Process provider, which SB07 owns.
