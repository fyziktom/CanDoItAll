# SB01 Semantic Invariants

## Invariant FAKE-DREAM-META-TEXT-01

- Invariant ID: `FAKE-DREAM-META-TEXT-01`
- Source raw note: Dream synthesis must store internalized knowledge, not meta-evidence template text.
- Expected behavior: Aggregate memory text is accepted when it says `Conclusion: rollout safety is supported by N source-backed observation(s)`.
- Disallowed shallow implementation: Treating diagnostic evidence-count templates as shipped knowledge.
- Failing-first test: `FakeProof.TemplateDreamMetaText` in `bundle://proof/SB01/transcripts/failing-first.txt`.
- Passing test: `FakeProof.TemplateDreamMetaText` in `bundle://proof/SB01/transcripts/passing.txt`.
- Changed source files: `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` with hash `079561F5460E68FAE447ECA8CD1D5072EEDB7CCD9A7FF09035131EAA92012D9A`.
- Production assertions: Dream synthesis emits the evidence-count template and the tests assert it is non-empty.
- Red-team negative case: A task-facing user receives diagnostic evidence text instead of domain guidance.
- Downstream dependency check: A downstream final closure would incorrectly trust template text as deep synthesis.
