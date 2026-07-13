# SB01 Semantic Invariants

- Invariant ID: `SB01-HYGIENE-GUARD`
- Source raw note: RH-001 and RH-002 required repository hygiene repairs without broad allowlists.
- Expected behavior: tracked transient bundle artifacts no longer fail the hygiene guard, and active test identifiers use behavior names instead of work-package IDs.
- Disallowed shallow implementation: disabling the repository scanners or excluding broad directories such as all `codex/` or all memory tests.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first.txt`
- Passing test: `bundle://proof/SB01/transcripts/passing.txt`
- Changed source files: `repo://tests/Unit/CanDoItAll.Tests.Unit/RepositoryTransientArtifactHygieneTests.cs`
- Production assertions: no production runtime behavior changed for this hygiene-only subbundle.
- Red-team negative case: `bundle://proof/SB01/transcripts/anti-stub.txt` proves no broad skip/return shortcut was introduced.
- Downstream dependency check: SB05 full unit proof depends on SB01 and is recorded at `bundle://proof/SB05/full-unit-suite.txt`.

