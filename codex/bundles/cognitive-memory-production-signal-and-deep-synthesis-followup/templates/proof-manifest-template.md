# Proof Manifest Template

- Changed files with SHA-256: list every changed source/test/skill/validator file.
- Semantic invariant contract: cite `bundle://proof/SBxx/semantic-invariants.md` or `.json`.
- Failing-first transcript: cite `bundle://proof/SBxx/transcripts/failing-first.txt` with non-zero exit code.
- Passing transcript: cite `bundle://proof/SBxx/transcripts/passing.txt` with exit code 0.
- Source assertions transcript: cite `bundle://proof/SBxx/transcripts/source-assertions.txt`.
- Anti-stub audit transcript: cite `bundle://proof/SBxx/transcripts/anti-stub.txt`.
- Test-seeding policy: prove production-only signals are not manually seeded by the passing tests unless the test is explicitly a repository migration/fixture test.

## Production Behavior Artifact Matrix

Required when the proof names a new production signal, state, record, or event.

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `ArtifactName` | `repo://...` production emitter or `bundle://...` source assertion transcript | `repo://...` production consumer or `bundle://...` transcript | scheduler/review/cleanup path that runs it automatically | adversarial test/transcript proving consumer-only code or manual test seeding is insufficient |
