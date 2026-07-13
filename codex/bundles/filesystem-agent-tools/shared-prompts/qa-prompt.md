# QA Prompt

Validate that agents can discover and use the filesystem tool family safely.

Check:

- Non-recursive and recursive listing behave differently.
- Folder copy/create descriptions are explicit.
- Hash, zip, and unzip tools are registered, classified, and template-seeded.
- Read-only agents do not receive or cannot execute mutation/archive-write behavior.
- No code path bypasses `IWorkspaceFileService` or `WorkspacePathPolicy`.
