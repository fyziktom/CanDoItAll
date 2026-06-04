# SB12 Semantic Invariants

- Invariant ID: `SB12-INVARIANT-001`
- Source raw note: `RQ-013` Final red-team closure must prove no hidden coupling, parity loss, policy weakening, or process-core scope creep remains.
- Expected behavior: MAF remains decoupled from Processes, Projects, and Workbench product-tool modules; first-party product tools remain owned by registered runtime providers; Tooling remains product-neutral; process provider parity and policy tests pass; the next phase is limited to process contracts/core foundation planning and excludes driver packs.
- Disallowed shallow implementation: Closing the bundle from prose while direct MAF product references, removed attach helper names, product-specific Tooling references, process tool parity loss, access-policy weakening, or driver-pack scope creep remain.
- Failing-first test: `bundle://proof/SB12/transcripts/adversarial-direct-maf-processes-reference-scan.txt` records a non-zero scan for the original direct MAF-to-Processes coupling.
- Passing test: `bundle://proof/SB12/transcripts/final-hidden-dependency-and-scope-scan.txt`, `bundle://proof/SB12/transcripts/targeted-unit-provider-policy-tests.txt`, `bundle://proof/SB12/transcripts/targeted-integration-provider-process-tests.txt`, `bundle://proof/SB12/transcripts/final-dotnet-build-slnx.txt`, `bundle://proof/SB12/transcripts/bundle-validator-prepared.txt`, and `bundle://proof/SB12/transcripts/bundle-validator-completed.txt`.
- Changed source files: `bundle://proof/SB12/source-assertions/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB12/source-assertions/final-red-team-source-assertions.txt`, `bundle://proof/SB12/source-assertions/manual-red-team-checklist.md`, and `bundle://proof/SB12/source-assertions/next-phase-cutline.md`.
- Red-team negative case: A future change that adds a direct MAF reference to `CanDoItAll.Modules.Processes`, restores hard-coded attach paths, moves product-specific references into Tooling, or adds process driver-pack implementation fails SB12 scans or provider/policy tests.
- Downstream dependency check: The bundle may close because SB12 red-team proof, targeted tests, full build, branch hygiene, cutline, and validators pass on the final source shape.
