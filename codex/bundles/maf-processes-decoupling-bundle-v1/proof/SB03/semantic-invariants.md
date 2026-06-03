# SB03 Semantic Invariants

- Invariant ID: `SB03-INV-001`
- Source raw note: MAF and Processes must be decoupled without simplifying or omitting behavior.
- Expected behavior: The subbundle preserves the stated runtime/process behavior while advancing the dependency inversion.
- Disallowed shallow implementation: Passing build by deleting process tools, weakening tests, or using counts instead of exact tool-name parity.
- Failing-first test: Must be captured when behavior or guardrails change.
- Passing test: Must be captured after implementation.
- Changed source files: Pending implementation.
- Production assertions: Pending implementation.
- Red-team negative case: Prove hidden dependency or missing tool would be caught.
- Downstream dependency check: Next subbundle entry gate must cite this proof.
