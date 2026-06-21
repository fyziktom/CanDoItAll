# SB01 Semantic Invariants

- Invariant ID: `SB01-I001`
- Source raw note: `N001`, `N002`
- Expected behavior: quick-created simple notes derive bounded `Title`, persist full multiline `Notes`, and expose full runtime `InlineText`.
- Disallowed shallow implementation: do not widen the title column, trim the body, or hide save errors with fallback text.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first.txt` reproduced `Npgsql.PostgresException : 22001`.
- Passing test: `bundle://proof/SB01/transcripts/passing-component-tests.txt` and `bundle://proof/SB01/transcripts/passing-browser-runtime-inline-text.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`, `repo://tests/CanDoItAll.Tests.Components/ProjectStructurePageSimpleMutationTests.cs`, `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectStructureCanvasFeedbackBundle.cs`.
- Production assertions: `bundle://proof/source-assertions.txt` shows `ResolveCreatedTitle`, `BuildSimpleNoteTitle`, and the `Notes` `TEXT` mapping.
- Red-team negative case: long first line plus additional body lines and symbols proves title derivation is independent from body storage.
- Downstream dependency check: `bundle://proof/SB02/transcripts/passing-browser-width.txt` passed using the full runtime `InlineText`.
