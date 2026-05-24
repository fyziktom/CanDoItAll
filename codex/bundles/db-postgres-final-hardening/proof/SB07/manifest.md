# SB07 proof manifest

## Status

Completed.

## Changed files

| File | SHA-256 before | SHA-256 after | Reason |
|---|---|---|---|
| `tests/CanDoItAll.Tests.Unit/DatabaseCanonicalityArchitectureTests.cs` | new | See `../SB08/transcripts/changed-file-hashes.txt` | Encode approved maintenance/bootstrap boundaries for profile-specific context factory usage. |
| `docs/postgresql-runtime-canonicality.md` | new | See transcript | Document canonical runtime profile and pending restart behavior. |
| `docs/README.md` | See transcript | See transcript | Link canonical runtime documentation. |

## Commands

| Command | Result | Transcript |
|---|---|---|
| Unit architecture test filter | Passed, 1 test | `transcripts/unit-architecture-test.txt` |
| Runtime pending restart integration test | Passed, 1 test | `transcripts/runtime-pending-restart-test.txt` |
| Component database profile tests | Passed, 2 tests | `transcripts/component-database-profile-tests.txt` |

## Source assertions

| Assertion | Source | Proof |
|---|---|---|
| `IProfileAppDbContextFactory` is restricted to explicit maintenance/bootstrap/transfer boundaries. | `DatabaseCanonicalityArchitectureTests.cs` | Unit architecture test transcript. |
| Runtime activation is pending-restart, not hot switch. | Existing integration/component tests | Runtime/component transcripts. |
| Canonical runtime behavior is documented for future agents. | `docs/postgresql-runtime-canonicality.md` | Documentation file. |

## Negative tests

| Scenario | Expected | Result |
|---|---|---|
| New runtime module starts using profile-specific context factory directly. | Architecture test fails unless path is approved. | Passed. |
| Database switch changes current runtime profile without restart. | Integration test rejects it. | Passed. |

## Remaining risks

The allow-list is intentionally explicit. New legitimate maintenance boundaries must update the architecture test with a reasoned path addition.
