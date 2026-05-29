# 03-workspace-file-and-folder-executor-expansion

## Objective

Make local workspace file/folder workflows practical.

## Required work

1. Decide whether to extend `storage.file` or split into `workspace.file` and `workspace.folder`.
2. Add operations:
   - Exists
   - Tree
   - CreateDirectory
   - EnsureDirectory
   - DeleteFile
   - DeleteDirectory
   - CopyFile
   - MoveFile
   - CopyDirectory
   - MoveDirectory
   - Rename
   - Hash
   - ZipDirectory
   - UnzipArchive
3. Add settings:
   - source path
   - destination path
   - recursive
   - dry run
   - include/exclude glob patterns
   - max files
   - max bytes
   - overwrite behavior
4. Keep default operations workspace-scoped only.
5. Add file/folder result schemas with normalized paths and metadata.
6. Add deterministic tests against sandbox workspace.

## Acceptance checklist

- Users can select/list/read/write/create/copy/move/delete workspace folders safely.
- Deletion supports dry run and requires explicit recursive confirmation for directories.
- Path traversal and absolute path escape are tested.
