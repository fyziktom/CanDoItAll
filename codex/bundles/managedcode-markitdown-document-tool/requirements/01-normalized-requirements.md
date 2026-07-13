# Normalized Requirements

| Id | Requirement | Validation |
| --- | --- | --- |
| R001 | Add `ManagedCode.MarkItDown` to `CanDoItAll.Tools.Documents`. | Project restore/build includes the package. |
| R002 | Implement document-to-markdown conversion in `CanDoItAll.Tools.Documents`. | Direct converter unit tests pass. |
| R003 | Keep Core free of the concrete MarkItDown dependency. | Dependency scan shows Core does not reference Tools.Documents or ManagedCode.MarkItDown. |
| R004 | Replace `workspace_convert_document` Python execution path with the C# converter. | Artifact service tests use fake converter and no process execution. |
| R005 | Preserve public tool contract, receipts, preview, and output file behavior. | Existing/new `WorkspaceArtifactToolServiceTests` pass. |
| R006 | Register converter in hosting and module composition. | DI validation tests pass. |
| R007 | Keep image assets rejected with explicit guidance. | Existing image-rejection test passes. |
| R008 | Validate with the live 5032 app and project-structure floating agent chat. | Browser/API evidence recorded in execution report. |

