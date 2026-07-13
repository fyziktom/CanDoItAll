# SB01 Semantic Invariants

## SB01-INV-001

- Invariant ID: `SB01-INV-001`
- Source raw note: Add `ManagedCode.MarkItDown` as the C# replacement for Python MarkItDown in the documents tools project.
- Expected behavior: The converter accepts a source path and output path, calls ManagedCode.MarkItDown, writes markdown, and returns explicit conversion diagnostics.
- Disallowed shallow implementation: A package reference without a real converter, a converter that returns canned markdown, or a silent fallback to Python.
- Failing-first test: N/A process - no pre-change failing transcript was captured for the new converter; missing-source behavior is covered as a negative unit test.
- Passing test: `bundle://proof/SB01/transcripts/passing-converter-tests.log`
- Changed source files: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Tools/WorkspaceDocumentMarkdownConverter.cs`; `repo://src/MAF/Tools/CanDoItAll.Tools.Documents/Markdown/ManagedCodeMarkItDownDocumentMarkdownConverter.cs`; `repo://src/MAF/Tools/CanDoItAll.Tools.Documents/CanDoItAll.Tools.Documents.csproj`
- Production assertions: Core exposes only typed request/result abstractions; Tools.Documents owns the concrete SDK dependency.
- Red-team negative case: Missing source returns a failed result with diagnostics instead of throwing through the agent tool.
- Downstream dependency check: Core does not reference Tools.Documents or ManagedCode.MarkItDown.
