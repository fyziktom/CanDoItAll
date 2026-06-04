# Executor Catalog Target

## P0/P1 executors to implement first

### WorkspaceFileFolderExecutor

Extend `storage.file` or split into:

- `workspace.file`
- `workspace.folder`

Required operations:

- `Exists`
- `List`
- `Tree`
- `Stat`
- `ReadText`
- `WriteText`
- `AppendText`
- `ReadBytesAsBase64` or `ReadFileReference`
- `WriteBytesFromBase64` or `WriteFileReference`
- `CreateDirectory`
- `EnsureDirectory`
- `DeleteFile`
- `DeleteDirectory`
- `CopyFile`
- `MoveFile`
- `CopyDirectory`
- `MoveDirectory`
- `Rename`
- `Hash`
- `ZipDirectory`
- `UnzipArchive`

Guardrails:

- All default operations must stay inside workspace scope.
- Dangerous operations require explicit settings and tests.
- Delete directory must require `recursive` and optional `dryRun`.
- Absolute external paths must be a separate approval-gated import capability.

### JsonTransformExecutor

Operations:

- `Select`
- `Set`
- `Remove`
- `Merge`
- `ArrayMap`
- `ArrayFilter`
- `ArraySort`
- `ArrayDistinct`
- `ArrayTake`
- `AggregateCount`
- `TemplateObject`
- `ValidateSchema`

Use built-in safe JSON path and declarative transforms only.

### MarkdownRenderExecutor

Operations:

- `Template`
- `JsonToTable`
- `DocumentsToReport`
- `TasksToChecklist`
- `EvidenceTable`
- `WriteToWorkspaceFile`

### DelayAndControlExecutor

Operations:

- `Delay`
- `NoOp`
- `Fail`
- `Assert`
- `GateByBoolean`
- `EmitEvent`
- `WaitForExternalSignal` (future if durable backend supports it)

### ApprovalExecutor

Wrap the existing external request mechanism into a reusable executor for workflows that want explicit approval without using a `HumanInput` node kind.

### HttpDownloadExecutor

Either extend `http.fetch` or add `http.download`:

- save response body to workspace path,
- record file artifact,
- support content-type and max byte constraints,
- block private network by default unless allowed,
- use `IHttpClientFactory`.

## Later executors

- `document.extract` for DOCX/PPTX/HTML/ZIP/CSV/PDF metadata.
- `subworkflow.run`.
- `agent.step`.
- `table.transform`.
- `email.send/draft` with approval.
- `notification.send`.
- `scheduler.schedule`.
