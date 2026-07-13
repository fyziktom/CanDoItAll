# SB03 Semantic Invariants

## SB03-INV-001

- Invariant ID: `SB03-INV-001`
- Source raw note: Validate in the 5032 project-structure floating agent chat that the quotation PDF can be read and converted by the agent tool path.
- Expected behavior: The live agent reads the PDF asset, calls `workspace_convert_document`, receives markdown preview content, and extracts model/price data from that markdown.
- Disallowed shallow implementation: Only running unit tests, using a hard-coded quotation answer, or bypassing the agent-visible workspace tool.
- Failing-first test: N/A process - the E2E proof was run after implementation; the discovered approval-continuation blocker is tracked separately.
- Passing test: `bundle://proof/SB03/transcripts/passing-live-conversion.log`
- Changed source files: `repo://tests/Unit/CanDoItAll.Tests.Unit/WorkspaceArtifactToolServiceTests.cs`; `repo://tests/Unit/CanDoItAll.Tests.Unit/ManagedCodeMarkItDownDocumentMarkdownConverterTests.cs`
- Production assertions: Live logs include asset read and document conversion tool invocation after 5032 restart.
- Red-team negative case: Node creation did not complete after approval, proving the report separates conversion success from mutation continuation failure.
- Downstream dependency check: Browser proof exercised the same project-structure floating chat route used by the original bug report.
