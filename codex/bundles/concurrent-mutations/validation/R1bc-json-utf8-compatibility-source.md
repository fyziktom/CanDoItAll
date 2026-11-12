# Parked JSON UTF-8 write: compatibility proof source

Status: two new Unit Facts authored in the managed checkout; not built or executed. Production remains parked and unchanged by this slice. This is separate from historical receipt-read, browser and allocation-diagnostic work. The parent owns integration, standard Release builds, discovery, execution, portability-static and cleanup qualification.

## Boundary and smallest added proof

`FileSandboxWorkspaceJsonStore.WriteJsonAtomicallyAsync<T>` now has a parked direct UTF-8 serialization path for non-usage payloads. The previous path serialized with the store's Web/indented options to a string, then called `DurableFileWriter.WriteTextAsync`; that writer already delegates `Encoding.UTF8.GetBytes(content)` to `WriteBytesAsync`. Provider usage must retain its observed string path because the usage index and history journal participate in that write.

The new `FileSandboxWorkspaceJsonWriteCompatibilityTests.cs` has exactly two Facts:

1. A real `ExecutionRunRecord`, through both concrete and object-typed generic inputs, must produce exactly the bytes persisted by the previous string/text-writer route with the same serializer options. The payload includes Czech, Chinese and supplementary Unicode, HTML-sensitive characters, quotes, newlines/tabs, nested JSON strings, seven-digit fractional timestamps, and the real `AgentExecutionOperationId` converter. Parsed and reopened values must preserve runtime fields and the complete serialized record, with no BOM. An actual durable-writer `BeforeCommit` fault must leave the complete prior bytes readable from a new store, report the same primary failure, never reach `Committed`, and leave no temporary/pending publication file.
2. An object-typed real `ProviderUsageObservation` must still enter the observed usage path. The actual index and history journal are attached. A fault at `FileHistoryCommitStage.SourceCommitted` occurs after canonical source publication and before history completion. Exact source bytes must match the old string route; a new store reads the complete usage, the locator bucket and hash remain consistent, and a fresh history journal recovers the matching committed source into one publication. Its provider/model/token/price values must match, private raw usage/diagnostics must not enter the projection, and acknowledgement drains history without changing source bytes.

The object-typed cases check runtime type preservation with real persisted types; they do not invent a polymorphic storage hierarchy or claim coverage of every model/converter. The history assertion depends on the real journal comparing its prepared hash with the actual source bytes; it is not an assertion that a serialization API was called. Existing file-owner tests retain broader crash, deletion, expiry, index and history/SQL handoff proof.

## Source-derived validation inventory

Counts below come from C# method attributes and explicit InlineData in the frozen source, without runner enumeration or data-provider execution. There are no MemberData/ClassData rows or skips in these selected files. The new file has no category trait. `DurableFilePublicationTests` is HostPlatform; the file-owner Integration class is both FileSystemPortability and HostPlatform. Some existing durable-writer platform arrangements return on the other OS; a green row alone does not establish the nonexecuted platform behavior.

| Assembly/class | Facts | InlineData rows | Expected cases |
| --- | ---: | ---: | ---: |
| Unit: `FileSandboxWorkspaceJsonWriteCompatibilityTests` (new) | 2 | 0 | 2 |
| Unit: `FileSandboxWorkspaceJsonBatchTests` | 3 | 2 | 5 |
| Unit: `DurableFileWriterTests` | 15 | 2 | 17 |
| Unit: `DurableFilePublicationTests` | 4 | 4 | 8 |
| Unit: `FileProviderUsageWindowTests` | 7 | 8 | 15 |
| Integration: `FileSandboxWorkspaceUsageProjectionIntegrationTests` | 7 | 8 | 15 |
| Total | 38 | 24 | 62 |

The Unit union is **47** cases, Integration is **15**. The narrow new regression filter is `FullyQualifiedName~FileSandboxWorkspaceJsonWriteCompatibilityTests`, exactly **2**. The existing broader ProviderHistory persistence, capture and projection classes are not changed or required merely to reprove this private JSON encoding boundary; the selected file-owner class already covers actual history/SQL handoff and redaction. Run additional classes only if a failure or new change invalidates them.

The Unit union filter is:

```text
FullyQualifiedName~FileSandboxWorkspaceJsonWriteCompatibilityTests|FullyQualifiedName~FileSandboxWorkspaceJsonBatchTests|FullyQualifiedName~DurableFileWriterTests|FullyQualifiedName~DurableFilePublicationTests|FullyQualifiedName~FileProviderUsageWindowTests
```

The Integration filter is `FullyQualifiedName~FileSandboxWorkspaceUsageProjectionIntegrationTests`. It requires the parent's existing explicitly isolated PostgreSQL 18 fixture; the two new Unit Facts do not create a database or start a host. Refresh the owning Persistence Release output and Unit build-backed discovery after production is deliberately selected. Do not execute the parked optimization on a mismatched baseline binary or infer performance improvement from compatibility results.

## Ownership and evidence limits

Each new Fact owns one `CanDoItAllTestEnvironment` and its child `workspace` directory. Awaited environment disposal is the existing cleanup owner; all actual streams, JSON documents and writer operations are awaited/disposed before it. No raw user data, external provider, ordinary application, server, process or global configuration is involved. No fixture root was created during source preparation.

Only the new test, this proof and its source inventory are authored. The inventory records the main and managed parked JSON-store hashes as context, not as a production edit or authorization to transfer that file. All selected existing regression files remain unchanged. Roslyn parsing reports zero syntax errors for the new file; this does not establish type checking or passing tests. No build, test, database access, performance execution, scanner or commit was performed. Portability-static and runtime cleanup proof remain parent gates.
