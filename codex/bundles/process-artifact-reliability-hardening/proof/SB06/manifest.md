# SB06 Proof Manifest

## Status

Completed.

## Source Assertions

- Focused integration validation is captured in `bundle://proof/SB06/transcripts/focused-integration-tests.txt`.
- Full solution build validation is captured in `bundle://proof/SB06/transcripts/solution-build.txt`.
- PostgreSQL model/migration scope audit is captured in `bundle://proof/SB06/transcripts/postgresql-model-audit.txt`.
- SQLite residue audit is captured in `bundle://proof/SB06/transcripts/sqlite-residue-audit.txt`.
- Semantic invariant contract: `bundle://proof/SB06/semantic-invariants.md`.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| Process runtime validation suite | `dotnet test` command in `bundle://proof/SB06/transcripts/focused-integration-tests.txt` | Bundle closure and process dispatch regression review | Runs the full `ProcessRunAutomationDispatchServiceTests` class after SB01-SB05 changes | Test headers include red-team cases for wrong producer, placeholder, missing artifact, workflow artifact, and manager eligibility |
| PostgreSQL-only closure audit | `git diff --name-only` and SQLite residue command transcripts | Bundle closure report | Confirms no Process persistence or PostgreSQL migration files changed and no SQLite references were introduced by this change set | `bundle://proof/SB06/transcripts/sqlite-residue-audit.txt` rejects SQLite residue |

## Failing-First Proof

- Failing-first: N/A for this process validation subbundle; SB06 closes the already implemented process runtime hardening with regression, build, PostgreSQL scope, and SQLite-residue proof.

## Passing Proof

- Transcript path: `bundle://proof/SB06/transcripts/focused-integration-tests.txt`
- Transcript path: `bundle://proof/SB06/transcripts/solution-build.txt`
- Transcript path: `bundle://proof/SB06/transcripts/postgresql-model-audit.txt`
- Transcript path: `bundle://proof/SB06/transcripts/sqlite-residue-audit.txt`

## Anti-Stub Audit

- Transcript path: `bundle://proof/SB06/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

- Transcript path: `bundle://proof/SB06/transcripts/changed-file-hashes.txt`
- SHA-256 sample: `a6db909949d7b0ce1f40b092c5b1d45352b8c398fdbec145001ae3a83a2c0b1e`

## Blockers

None.
