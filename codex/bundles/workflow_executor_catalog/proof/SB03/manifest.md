# SB03 Proof Manifest

- Subbundle: `SB03`
- Status: `Completed`
- Owned requirements: R3, R4
- Raw notes: RN02, RN03
- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`

## Changed Source

| File | SHA-256 |
| --- | --- |
| `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Files/WorkspaceFileContracts.cs` | `BE87647E2CC160A7802EA29F06E77BB5836C7BEC30D99D2CE8947F32B96082FF` |
| `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Files/WorkspaceFileService.cs` | `E818A9C9E3D8A55AF66BC46A728FCB4363BE7599F712598C47C31F1F720960B3` |
| `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/WorkspaceFileWorkflowExecutor.cs` | `2F80E26349A3314EC3959852354CDAD75104D874EBC80AAF43919C4E9458F962` |
| `repo://src/CanDoItAll.AgentFramework.Models/Workspace/WorkspaceFileToolModels.cs` | `FF9FBBD13A1BD2F41E1C0798E896CFD8CD456DC3C70675CE48FF5FF0F511ACB5` |

## Command Transcripts

- Source assertions: `bundle://proof/SB10/transcripts/source-assertions-workspace-file-ops.txt`
- Failing-first: N/A - process/non-production exemption because SB03 filled audited operation gaps without preserving a failing fixture from the prior implementation.
- Passing transcript: `bundle://proof/SB10/transcripts/dotnet-test-unit-workflow-executor-catalog.txt`
- Passing scenario transcript: `bundle://proof/SB10/transcripts/dotnet-test-scenario-harness-workflow-executor-catalog.txt`
- Anti-stub audit: `bundle://proof/SB10/transcripts/anti-stub-audit-workflow-executor-catalog.txt`
- Changed-file hashes: `bundle://proof/SB10/transcripts/changed-file-hashes.txt`
- Test name: `WorkspaceFileExecutorSupportsDirectoryHashZipAndDryRunDelete`

## Closure Result

Workspace file/folder workflows now cover create, list/tree, exists, copy, move, delete, hash, zip, unzip, and bounded include/exclude scenarios through workspace-scoped services.
