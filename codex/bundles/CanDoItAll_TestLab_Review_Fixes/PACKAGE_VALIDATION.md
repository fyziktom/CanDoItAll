# Package validation

Checks executed when preparing this archive on 2026-09-28. These checks validate the handoff, **not CanDoItAll's implementation**.

| Check | Result |
| --- | --- |
| UTF-8 text and safe relative file inventory | Passed; Markdown, JSON, Python, SHA-256 manifests and two proposed C# test methods in one source file |
| Parsed JSON files | 7; all valid |
| Markdown local file/directory target occurrences | 105; all resolve within the archive |
| New source register | 32 unique IDs, inspected read scopes and valid reported Git object IDs |
| Original TestLab package | 33 of 33 files preserved byte-for-byte |
| Shared v3 within original package | 22 of 22 files preserved byte-for-byte |
| Original shared validator | Passed metadata, source IDs, local targets, inventory and sealed manifest |
| Original shared Python tooling tests | 14 passed; 0 failed; 0 skipped |
| Root SHA-256 manifest | All other archive files sealed and verified |
| ZIP file set, byte equality, CRC and member paths | Passed |
| Reviewed branch at closure | Re-read; unchanged `3c579fd1a923ad90f619fe144e6e4c1fe081fa8b` |

The Markdown check validates local file/directory targets; it does not certify external URL connectivity or section-fragment resolution. A GitHub-returned source blob ID is provenance, not a local product checkout verification.

**Not performed:** C# compilation, proposed regression-seed execution, product tests, PostgreSQL tests, browser execution, evaluated MSBuild graph or dotnet-watch measurements. The 115 product cases in the historical implementation record remain author-reported results. Python tooling checks above are unrelated to those product cases.

`regression-seeds/TestLabSandboxReviewRegressionTests.cs` is explicitly proposed, uncompiled source. Codex must adapt and execute it against current code and extend actual-control coverage as required by the matrix.
