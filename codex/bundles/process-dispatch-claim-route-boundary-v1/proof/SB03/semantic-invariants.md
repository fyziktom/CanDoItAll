# SB03 Semantic Invariants

- Invariant ID: `SB03-INV-001`
- Source raw note: RN-001, RN-003.
- Expected behavior: Concurrency extraction is designed around pure selection/classification only, while async service calls remain in dispatcher adapters.
- Disallowed shallow implementation: Moving `executionClient` calls or polling into a "pure" helper, or extracting only one happy-path selector without stale/current-attempt/recoverable/competing parity.
- Failing-first test: `bundle://proof/SB03/transcripts/sb03-inventory-completeness-check.txt` would fail if method coverage or async-adapter cutline entries are missing.
- Passing test: `bundle://proof/SB03/transcripts/sb03-inventory-completeness-check.txt` passed after the inventory was completed.
- Changed source files: None in production or test source for SB03.
- Production assertions: `bundle://proof/SB03/source-assertions/concurrency-selection-design.md`.
- Red-team negative case: `bundle://proof/SB03/transcripts/sb03-no-core-no-driver-no-ui-scan.txt` proves design-only work did not introduce Process Core, production driver API, or UI drift.
- Downstream dependency check: SB04 must add guardrails before SB05/SB06 move pure selection rules.
