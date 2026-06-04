# SB01 Semantic Invariants

- Invariant ID: SB01-INVARIANT-001
- Source raw note: RQ-001 preserve completed MAF -> Processes decoupling, and RQ-002 clean branch hygiene before more runtime work.
- Expected behavior: Historical codex/bundles/* proof bundles from development are not accidentally removed while the new decoupling and follow-up bundle artifacts remain available for review.
- Disallowed shallow implementation: Starting provider/runtime work while ignoring unrelated historical bundle deletions in the branch diff.
- Failing-first test: N/A for production behavior; branch hygiene is a non-production artifact correction. The branch baseline failure is captured in bundle://proof/SB01/transcripts/branch-diff-baseline.txt and shows the deleted historical bundle paths.
- Passing test: bundle://proof/SB01/transcripts/historical-bundle-restore-audit.txt, bundle://proof/SB01/transcripts/maf-hidden-dependency-scan.txt, and bundle://proof/SB01/transcripts/solution-build.txt.
- Changed source files: No production source files changed in SB01; proof and bundle files are hashed in bundle://proof/SB01/source-assertions/changed-file-hashes.txt.
- Production assertions: bundle://proof/SB01/source-assertions/branch-hygiene-source-assertions.txt and bundle://proof/SB01/transcripts/maf-hidden-dependency-scan.txt prove MAF production source/project files do not regain process dependency markers.
- Red-team negative case: bundle://proof/SB01/transcripts/branch-diff-baseline.txt demonstrates the accidental-deletion risk that SB01 rejects before downstream runtime work.
- Downstream dependency check: SB02 may start only after bundle://proof/SB01/transcripts/solution-build.txt succeeds and bundle://inventories/05-sb01-branch-hygiene-inventory.md records the restored historical bundle decision.
