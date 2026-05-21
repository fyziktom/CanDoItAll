# Proof Manifest Template

- Changed files with SHA-256: list every changed source/test/skill/validator file.
- Semantic invariant contract: cite `bundle://proof/SBxx/semantic-invariants.md` or `.json`.
- Failing-first transcript: cite `bundle://proof/SBxx/transcripts/failing-first.txt` with non-zero exit code.
- Passing transcript: cite `bundle://proof/SBxx/transcripts/passing.txt` with exit code 0.
- Source assertions transcript: cite `bundle://proof/SBxx/transcripts/source-assertions.txt`.
- Anti-stub audit transcript: cite `bundle://proof/SBxx/transcripts/anti-stub.txt`.
- Producer assertions: list production emitter paths for every new signal/state/record.
- Consumer assertions: list production consumer paths for every new signal/state/record.
- Lifecycle assertions: list scheduler/review/cleanup paths where behavior must run automatically.
- Test-seeding policy: prove production-only signals are not manually seeded by the passing tests unless the test is explicitly a repository migration/fixture test.
