# SB01 Semantic Invariants

## SB01-INV-001

- Invariant ID: `SB01-INV-001`
- Source raw note: `N008`
- Expected behavior: Catalogue and preview dialog proposals exist as durable bundle artifacts.
- Disallowed shallow implementation: Claiming proposals were generated without storing workspace-accessible artifacts.
- Failing-first test: N/A process/non-production exemption; SB01 changes no production behavior.
- Passing test: Prepared validator and design artifact hash checks passed.
- Changed source files: Bundle artifacts only; see `bundle://proof/SB01/transcripts/sb01-bundle-artifact-hashes.txt`.
- Production assertions: N/A because SB01 introduced no production code path.
- Red-team negative case: Generated design images are not accepted as shipped UI proof; SB04 must capture real browser screenshots.
- Downstream dependency check: SB02-SB04 cite these proposal paths for implementation and screenshot comparison.

## SB01-INV-002

- Invariant ID: `SB01-INV-002`
- Source raw note: `N013`
- Expected behavior: Large-screen-only validation policy is explicit before implementation begins.
- Disallowed shallow implementation: Silently omitting responsive proof without recording the user constraint.
- Failing-first test: N/A process/non-production exemption; SB01 records validation policy only.
- Passing test: Structured input and phase plan record the small/medium viewport skip.
- Changed source files: Bundle artifacts only; see `bundle://proof/SB01/transcripts/sb01-bundle-artifact-hashes.txt`.
- Production assertions: N/A because SB01 introduced no production code path.
- Red-team negative case: Final browser analytics must not claim small/medium viewport validation.
- Downstream dependency check: SB04 closure records the large-screen-only browser proof.
