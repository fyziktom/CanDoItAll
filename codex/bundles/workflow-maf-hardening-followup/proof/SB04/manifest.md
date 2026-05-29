# SB04 proof manifest

- Subbundle: `SB04`
- Status: `Completed`
- Owned requirements: R4, R5
- Raw notes: workflow checkpointing must establish a trusted metadata/storage abstraction, remain compatible with HITL request state, and avoid implying that in-process preview execution is resumable production durability.
- Semantic invariant contract: `bundle://proof/SB04/semantic-invariants.md`

## Changed File Manifest

After hashes are captured in `bundle://proof/SB04/transcripts/changed-file-hashes.txt`.

| File | After SHA-256 |
| --- | --- |
| `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowModels.cs` | `3CC9CDB74D72C3F27DFB5E257637F48F44F00A076557E3CD54ED99B99401BFAC` |
| `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowIdJsonConverters.cs` | `10CC2FEA390335BF7DA7F9D4A84EFB7001F48EAA517070EAF85FFB939A195BF9` |
| `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowCatalogModels.cs` | `A08FADAE75FEC34C51AED0059BD6C51F138F97913935E2A753104C30B8424D5E` |
| `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowContracts.cs` | `3FDF195A067A633B05B4C24298BA4F2B738C81AA35D8A113F94215A3D4B88796` |
| `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowRuntimeManager.cs` | `6D0EA69B0244EECF074CA7F10C1C25EE5587D83F9C28CD047FA5F9DD3415490D` |
| `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowCatalogServices.cs` | `BEE015AC41D5C9B7CAAB2CC31C7CB1CFC1ED2AEFB3F1C2B80A8991BD543F3E3C` |
| `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs` | `CE11C0E2781E32BA0E0FCD2AA0E114C296BE3044AA78ADE2DB641364622B544A` |
| `repo://src/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs` | `3D1CDBD6B5C555719A4CBEDE07E8E7625B914772A3748BF6FEC7CD0A65F5045A` |
| `repo://src/CanDoItAll.Web/Api/WorkflowsApi.cs` | `791930200895C89587B0B9476ACC8F2750CFB26167E5A91D9B4EB7C00622FDD2` |
| `repo://src/CanDoItAll.Migrations.PostgreSql/Migrations/20260529111314_AddWorkflowCheckpoints.cs` | `BFB4B08E3921A62339CC0EFD5131B52CFCFE38F4BA3C947913A85E1FF36A0042` |
| `repo://docs/workflow-maf-hardening.md` | `029C9D46FAADA5AA7F9E8EC17FC26B966F86BF1259B847F63526CAA075DD3820` |
| `repo://tests/CanDoItAll.Tests.Unit/WorkflowFoundationTests.cs` | `725542FC2F71EAA234C44BE6CC286B83E2E21CF3918CC4BFDD09FDDCDA5E8F85` |
| `repo://tests/CanDoItAll.Tests.Integration/WorkflowApiIntegrationTests.cs` | `BEC68ACA78487016A22B311179D7E0F455593BC944FB26B8BA10B8A71135B7FA` |

## Command Transcripts

- Failing-first checkpoint proof: `bundle://proof/SB04/transcripts/failing-first-checkpoint-tests.txt`
- Passing unit checkpoint/runtime proof: `bundle://proof/SB04/transcripts/unit-workflow-foundation-checkpoints-after-implementation.txt`
- Passing API integration proof: `bundle://proof/SB04/transcripts/integration-workflow-api-checkpoints-after-implementation.txt`
- Passing component smoke proof: `bundle://proof/SB04/transcripts/component-workflows-page-smoke-after-checkpoints.txt`
- Passing solution build proof: `bundle://proof/SB04/transcripts/solution-build-slnx-after-checkpoints.txt`
- Semantic invariant index: `bundle://proof/SB04/transcripts/semantic-invariant-evidence.txt`

## Source Assertions

- Source-level assertion transcript: `bundle://proof/SB04/transcripts/source-assertions-checkpoints.txt`
- Checkpoint model and JSON id converter: `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowModels.cs`
- Store/factory abstractions and explicit resume-unavailable metadata: `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowContracts.cs`
- In-memory trusted test storage and persistence path: `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowRuntimeManager.cs`
- MAF in-process runtime capture at completed/failed/waiting boundaries: `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`
- EF-backed checkpoint table and PostgreSQL migration: `repo://src/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs`; `repo://src/CanDoItAll.Migrations.PostgreSql/Migrations/20260529111314_AddWorkflowCheckpoints.cs`
- API-facing checkpoint state: `repo://src/CanDoItAll.Web/Api/WorkflowsApi.cs`; `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowCatalogModels.cs`
- Trust-boundary documentation: `repo://docs/workflow-maf-hardening.md`

## Anti-Stub Audit

- Anti-stub transcript: `bundle://proof/SB04/transcripts/anti-stub-audit-checkpoints.txt`
- The implementation does not load native checkpoint blobs, does not use `FileSystemJsonCheckpointStore` in the workflow runtime path, and does not expose raw checkpoint payloads in normal API/UI surfaces.
- Resume is intentionally `NotSupported` for metadata-only in-process checkpoints. This is explicit API state, not a hidden fallback.

## Downstream Smoke Proof

- Workflow API smoke: `bundle://proof/SB04/transcripts/integration-workflow-api-checkpoints-after-implementation.txt`
- Workflow component smoke: `bundle://proof/SB04/transcripts/component-workflows-page-smoke-after-checkpoints.txt`
- Solution build: `bundle://proof/SB04/transcripts/solution-build-slnx-after-checkpoints.txt`

## Known Residuals

- `dotnet build CanDoItAll.slnx --no-restore` still reports existing EF Core Relational `MSB3277` version-conflict warnings; it exits successfully with zero errors.
- The in-process MAF backend persists metadata-only checkpoints. Durable native resume remains unavailable until a durable workflow backend writes trusted runtime state.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Checkpoint metadata record | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs` | `repo://src/CanDoItAll.Web/Api/WorkflowsApi.cs` | `bundle://proof/SB04/transcripts/unit-workflow-foundation-checkpoints-after-implementation.txt` | `bundle://proof/SB04/transcripts/failing-first-checkpoint-tests.txt`; `bundle://proof/SB04/transcripts/unit-workflow-foundation-checkpoints-after-implementation.txt` |
| Resume availability state | `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowContracts.cs` | `repo://src/CanDoItAll.Web/Api/WorkflowsApi.cs` | `bundle://proof/SB04/transcripts/integration-workflow-api-checkpoints-after-implementation.txt` | `bundle://proof/SB04/transcripts/integration-workflow-api-checkpoints-after-implementation.txt` |
