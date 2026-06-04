# SB02 Semantic Invariants

- Invariant ID: `SB02-INV-001`
- Source raw note: "Keep all original functions and prove no behavior was dropped."
- Expected behavior: SB02 inventories the live validation methods, current side-effect indicators, helper candidates, and test anchors before any rule movement.
- Disallowed shallow implementation: Designing extraction from stale seed rows without checking the current 3931-line source file.
- Failing-first test: N/A because SB02 is inventory-only and changes no production behavior.
- Passing test: `bundle://proof/SB02/transcripts/method-inventory.txt`, `bundle://proof/SB02/transcripts/side-effect-scan.txt`, and `bundle://proof/SB02/transcripts/test-surface-scan.txt`
- Changed source files: N/A.
- Production assertions: `bundle://proof/SB02/source-assertions/method-and-side-effect-inventory.md`
- Red-team negative case: The side-effect scan prevents moving file-system probing/copying into pure rule helpers by accident.
- Downstream dependency check: SB03 may start because pure validation families and dispatcher-owned side effects are explicitly separated.

- Raw note owned: Inventory current validation behavior before extracting it.
- Shipped behavior: No production behavior changed in SB02.
- Source proof: `bundle://proof/SB02/source-assertions/method-and-side-effect-inventory.md`
- Test proof: `bundle://proof/SB02/transcripts/test-surface-scan.txt`
- Shallow-pass trap: Treating the seed inventory as current truth.
- Adversarial negative proof: `bundle://proof/SB02/transcripts/side-effect-scan.txt`
- Semantic positive proof: `bundle://inventories/02-artifact-validation-method-inventory-seed.md`
- Anti-stub audit: N/A, inventory-only; no production code changed.
