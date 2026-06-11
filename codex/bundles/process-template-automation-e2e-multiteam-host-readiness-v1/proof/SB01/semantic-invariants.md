# SB01 Semantic Invariants

- Invariant ID: `SB01_INV_005`
- Source raw note: Review real code and tests, not only bundle claims; keep the bundle code-first.
- Expected behavior: The executable guard accepts closure only when source plus test changed lines are at least five times bundle changed lines.
- Disallowed shallow implementation: A four-to-one dominance rule or report-only statement could allow proof-heavy closure.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first-source-assertion.txt` shows the `HEAD` baseline lacked the 5x multiplier.
- Passing test: `bundle://proof/SB01/transcripts/focused-test.txt` shows `Process_runtime_host_codefirst_SB01_INV_005_numstat_summary_accepts_exact_five_to_one_source_test_dominance` passed.
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs` before SHA-256 `68BA8C3E45D60F52532430D281CE97FA41DB317AA0303E0B46828C694B63DBAC`, after SHA-256 `2E26F49903EC61823981D74672B2AB7C1FFEB82B5D0FF310BF15DB3641E737B5`.
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions.txt` shows the 5x multiplier and exact boundary test in the integration guard.
- Red-team negative case: `bundle://proof/SB01/transcripts/failing-first-source-assertion.txt` rejects the old 4x guard.
- Downstream dependency check: SB02 may proceed because the executable code-first guard now matches this bundle's final ratio contract.
