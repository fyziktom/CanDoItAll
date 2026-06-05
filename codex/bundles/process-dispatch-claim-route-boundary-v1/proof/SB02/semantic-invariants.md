# SB02 Semantic Invariants

- Invariant ID: `SB02-INV-001`
- Source raw note: RN-001, RN-003.
- Expected behavior: Before production movement, every high-risk dispatch branch is classified by source range, decision, side effect, helper candidate, and proof need.
- Disallowed shallow implementation: Leaving the seeded route list in place or treating side-effecting claim/route flows as pure planner logic.
- Failing-first test: `bundle://proof/SB02/transcripts/sb02-inventory-completeness-check.txt` would fail if required branch entries or the no-side-effect planner cutline were missing.
- Passing test: `bundle://proof/SB02/transcripts/sb02-inventory-completeness-check.txt` passed after inventory completion.
- Changed source files: None in production or test source for SB02.
- Production assertions: `bundle://proof/SB02/source-assertions/dispatch-route-inventory.md`.
- Red-team negative case: `bundle://proof/SB02/transcripts/sb02-no-core-no-driver-no-ui-scan.txt` proves the inventory-only phase did not introduce Process Core, production driver API, or UI drift.
- Downstream dependency check: SB03 and later implementation phases must preserve the side-effect cutline in `bundle://inventories/02-current-dispatch-route-map.md`.
