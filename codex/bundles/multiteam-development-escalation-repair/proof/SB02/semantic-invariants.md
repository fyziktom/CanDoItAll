# SB02 Semantic Invariants

## Invariant MTE-SB02-CONTRACTS

- Invariant ID: `MTE-SB02-CONTRACTS`
- Source raw note: Architects must prepare architecture and must not write the app; implementation and repair lanes own product mutation.
- Expected behavior: Software-delivery subprocess launcher steps stay external-action controlled, architecture steps stay read-only, implementation child steps own mutation, and QA must compare visual ImageAsset targets with screenshots.
- Disallowed shallow implementation: Adding broad tools to every agent or allowing architecture/QA to mutate product files to avoid escalation.
- Failing-first test: `bundle://proof/SB02/transcripts/failing-first.txt` records the contract mismatch before repair.
- Passing test: `bundle://proof/SB02/transcripts/passing.txt` records focused template projection and prompt tests passing.
- Changed source files: `repo://Templates/Processes/processes/software-delivery/definition.json` with hash `4AA1A1AA454BEB92441E86407F4160C1E2B7E913C35EE98399FAE64EE9B60FA0`; `repo://Templates/Processes/processes/dotnet-feature-function-implementation/definition.json` with hash `808D0107BE6476EB0C79AAA16F1530E473C497FF493984DD1A2BE2A927F3590C`.
- Production assertions: `software-delivery` QA and QA recheck contracts require `Visual target comparison`, source ImageAsset evidence, media path evidence, and screenshot refs.
- Red-team negative case: A template that grants product mutation to architecture or omits the image-target comparison contract would fail the focused projection tests.
- Downstream dependency check: SB03 launch semantic validation and SB04 live proof depend on these corrected contracts.
