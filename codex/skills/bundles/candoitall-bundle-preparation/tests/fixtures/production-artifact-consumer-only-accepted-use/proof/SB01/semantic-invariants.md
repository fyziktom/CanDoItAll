# SB01 Semantic Invariants

## Invariant FAKE-ACCEPTED-USE-01

- Invariant ID: `FAKE-ACCEPTED-USE-01`
- Source raw note: Accepted professor use must be a real production signal, but this fixture only proves enum, consumer, and test seed evidence.
- Expected behavior: The new `ProfessorAnchorAcceptedUse` signal is treated as complete when the evaluator consumes it and tests manually seed signal rows.
- Disallowed shallow implementation: Accepting a new production signal without a production producer, lifecycle wiring, or a negative test proving manual test seeding is not the only path.
- Failing-first test: `FakeProof.AcceptedUseConsumerOnly` in `bundle://proof/SB01/transcripts/failing-first.txt`.
- Passing test: `FakeProof.AcceptedUseConsumerOnly` in `bundle://proof/SB01/transcripts/passing.txt`.
- Changed source files: `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` with hash `079561F5460E68FAE447ECA8CD1D5072EEDB7CCD9A7FF09035131EAA92012D9A`.
- Production assertions: `ProfessorAnchorAcceptedUse` exists as an enum value, the assimilation evaluator consumes it, and tests can seed it directly.
- Red-team negative case: Mere recall without an accepted workflow outcome is not tested here.
- Downstream dependency check: A downstream final closure would incorrectly trust accepted-use assimilation from consumer-only proof.
