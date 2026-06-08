# Red-Team Fake-Proof Review

- Scope: SB003, SB006, SB009, SB012, SB015, SB018, SB021, SB024, SB027, SB030, SB033, SB036, SB039, SB042, SB045.
- Fake-proof trap rejected: table-only or prose-only completion without source, tests, source scans, and changed-file hashes.
- Negative proof: bundle://proof/SB012/transcripts/failing-first-alpha-tests.txt shows the focused verifier tests failed before the alpha package existed.
- Positive proof: bundle://proof/SB012/transcripts/passing-alpha-tests.txt verifies .NET/Rust diagnostics, denials, audit/redaction, hash policy, no runtime hook, and docs.
- Broad proof: bundle://proof/SB040/transcripts/passing-solution-build.txt and bundle://proof/SB040/transcripts/passing-full-unit-tests.txt.
- Source proof: bundle://proof/SB041/transcripts/passing-source-scans.txt rejects forbidden runtime tokens, Core reverse dependency, stubs, and UI/media drift.
- Result: PASS - the bundle is not closed from status rows alone.
