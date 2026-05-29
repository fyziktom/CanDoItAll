# SB05 Semantic Invariants

## Invariant SB05-MARKDOWN-OUTPUT-TRUTH

- Source raw note: RN02 and R6 require report generation with file output and artifact integration.
- Expected behavior: markdown rendering is deterministic, table bindings are explicit, and configured output files are written before file artifacts are recorded.
- Disallowed shallow implementation: returning markdown text only while claiming file/report artifacts.
- Positive proof: `MarkdownRenderExecutorRendersTablesAndWritesOutputFile` and `MafBackendRecordsConfiguredFileArtifactsForCompletedFileWrites` in `bundle://proof/SB10/transcripts/dotnet-test-unit-workflow-executor-catalog.txt`
- Source proof: `bundle://proof/SB10/transcripts/source-assertions-executor-implementations.txt`
