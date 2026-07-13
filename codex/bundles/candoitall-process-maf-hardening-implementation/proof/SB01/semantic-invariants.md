# Semantic Invariants - SB01

## INV-SB01-01

- Invariant ID: `INV-SB01-01`
- Source raw note: user required analysis of all similar process/template/artifact trouble, not only `prepare-solution-skeleton`.
- Expected behavior: inventory covers every current subprocess parent and shared artifact-template audit scope.
- Disallowed shallow implementation: inventory covers only the blocked sample process.
- Failing-first test: `bundle://proof/SB09/transcripts/adversarial-negative.md`
- Passing test: `bundle://proof/SB01/transcripts/template-inventory.txt`, `bundle://proof/SB01/transcripts/source-test-inventory.txt`, and `bundle://proof/SB09/transcripts/final-validation.md`
- Changed source files: production source unchanged during SB01; later implementation hashes are in `bundle://proof/SB09/changed-file-hashes.md`.
- Production assertions: nine subprocess parent steps and shared artifact templates were inventoried before implementation.
- Red-team negative case: a newly discovered subprocess parent missing from inventory reopens SB01.
- Downstream dependency check: SB04/SB08 start from the nine-parent matrix and shared artifact audit scope.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Inventory artifacts | `bundle://inventories` | SB04/SB08 gates | Bundle phase plan | Missing parent row blocks progression |
