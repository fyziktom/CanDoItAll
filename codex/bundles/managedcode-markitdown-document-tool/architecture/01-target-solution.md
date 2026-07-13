# Target Solution

## Boundary Design

Add a Core abstraction:

- `IWorkspaceDocumentMarkdownConverter`
- `WorkspaceDocumentMarkdownConversionRequest`
- `WorkspaceDocumentMarkdownConversionResult`

The abstraction belongs in Core because `WorkspaceArtifactToolService` already lives there and must not reference a concrete document tool package.

Add a Tools.Documents implementation:

- `ManagedCodeMarkItDownDocumentMarkdownConverter`

This implementation owns the `ManagedCode.MarkItDown` NuGet dependency and uses `MarkItDownClient.ConvertAsync(path, cancellationToken)`.

## Runtime Wiring

Update service composition:

- Hosting `AddAgentFrameworkCore` registers `IWorkspaceDocumentMarkdownConverter`.
- Module host registers the same converter for scoped organization workspace services.
- `MafRuntimeDependencyResolver` fallback creates the converter explicitly when DI does not provide one.

Update `WorkspaceArtifactToolService`:

- Require `IWorkspaceDocumentMarkdownConverter`.
- Resolve and validate workspace input/output paths in the artifact service.
- Call converter with absolute source and output paths.
- Create the existing `WorkspaceDocumentConversionResult` and `workspace_convert_document` receipt.
- Preserve image rejection guidance.

## Performance Expectation

The new path avoids process startup and Python module import for each conversion. A reusable MarkItDown client also lets the library use its async and internal stream/disk-backed processing model. Conversion cost still depends on document complexity, especially PDF extraction, but the runtime should stop failing before work starts because of missing Python packages.

## Testability Expectation

The artifact service can be unit tested with a fake converter. The real converter can be tested directly with small deterministic files. DI can be smoke-tested without starting a provider runtime.

## Non-Goals

- Do not change broad project-structure mutation permissions in this bundle.
- Do not add margin-calculation or quotation-specific finance behavior.
- Do not move conversion code into Maf runtime classes.
- Do not keep a silent Python fallback for this tool.

