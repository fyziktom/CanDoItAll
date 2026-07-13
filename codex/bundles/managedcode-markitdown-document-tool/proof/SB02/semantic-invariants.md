# SB02 Semantic Invariants

## SB02-INV-001

- Invariant ID: `SB02-INV-001`
- Source raw note: Agents should reach document conversion through tools without relying on missing Python MarkItDown.
- Expected behavior: `workspace_convert_document` resolves workspace paths, calls `IWorkspaceDocumentMarkdownConverter`, writes previews and receipts, and returns explicit errors.
- Disallowed shallow implementation: Keeping the old `WorkspaceCommandExecutionService` MarkItDown branch, adding a Python fallback, or hiding conversion failures.
- Failing-first test: `bundle://proof/SB02/transcripts/failing-first-receipt-path.log`
- Passing test: `bundle://proof/SB02/transcripts/passing-workspace-tool-tests.log`
- Changed source files: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Tools/WorkspaceArtifactToolService.cs`; `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Files/WorkspaceFileReceiptWriter.cs`; `repo://src/MAF/Common/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs`
- Production assertions: The artifact service depends on the converter abstraction and conversion receipts use bounded filenames.
- Red-team negative case: Converter failure returns an explicit failed workspace result without pretending a preview exists.
- Downstream dependency check: Core depends only on abstractions; composition roots own the concrete implementation wiring.
