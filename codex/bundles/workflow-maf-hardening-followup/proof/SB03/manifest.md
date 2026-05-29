# SB03 proof manifest

- Subbundle: `SB03`
- Status: `Completed`
- Owned requirements: R2, R4, R5, R6
- Raw notes: persisted workflow event records must carry useful node/executor/request identity, redacted bounded payloads, and enough timeline context for HITL, debugging, checkpoints, and artifact policy.
- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`

## Changed File Manifest

After hashes are captured in `bundle://proof/SB03/transcripts/changed-file-hashes.txt`.

| File | After SHA-256 |
| --- | --- |
| `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowModels.cs` | `39427D1964A7D9B5B5DC723E59DF46D8FB602ECA56A0E073AB170CB4FC12ABAE` |
| `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExecutorObservability.cs` | `A28EEF6DB24BCC7C274CF2D582DDA8C2CAC1495A4A462FBB0AAB88F3DF9DFC76` |
| `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowEventPayloads.cs` | `0DD5C8EAA4BA2227A007488828F411126F61C23E71D6663497A860095F97C8A7` |
| `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowNodeExecutionProgress.cs` | `414CF11581B1CA80302B8B53E1B20FD0E409B7512896826315A1D07A6B0DCA22` |
| `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowRuntimeManager.cs` | `09F45C0F3580608628D6F6E3C97DDD6B4569032C04816A5B7DCEC5F7B8B597A6` |
| `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowEventNormalizer.cs` | `C34BCD3F74F5A74DADB3B41A78C9AF90234140028B5FDB14E4E81B84501E38AE` |
| `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs` | `C322AC43AE7580DD9154F03A527B811763E421AE798DFC2F5EEE69F76CE9AC41` |
| `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs` | `BBA9E9F735E6BC1FD387A4170D2EBB398DAB9B1786502919CF888252DBC1C698` |
| `repo://tests/CanDoItAll.Tests.Unit/MafWorkflowEventNormalizerTests.cs` | `D4B5C1F8E0039169D488531E498632B736EC971E0F17E45A98BD0C2DD191CF03` |
| `repo://tests/CanDoItAll.Tests.Integration/WorkflowApiIntegrationTests.cs` | `929142D20CBBC8DBA95CB7D2AC8B524A4AB3ED81EC3BAF0E6595650280FE0F98` |

## Command Transcripts

- Failing-first event normalizer proof: `bundle://proof/SB03/transcripts/failing-first-event-normalizer-tests.txt`
- Passing unit event/runtime proof: `bundle://proof/SB03/transcripts/unit-event-normalizer-after-implementation.txt`
- Passing API integration proof: `bundle://proof/SB03/transcripts/integration-workflow-api-event-envelope-after-implementation.txt`
- Passing component smoke proof: `bundle://proof/SB03/transcripts/component-workflows-page-smoke-after-event-normalizer.txt`
- Passing solution build proof: `bundle://proof/SB03/transcripts/solution-build-slnx-after-event-normalizer.txt`
- Semantic invariant index: `bundle://proof/SB03/transcripts/semantic-invariant-evidence.txt`

## Source Assertions

- Source-level assertion transcript: `bundle://proof/SB03/transcripts/source-assertions-event-normalizer.txt`
- Structured event payload envelope: `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowModels.cs`
- Payload redaction/bounds helper: `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowEventPayloads.cs`
- MAF event normalizer and binding index: `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowEventNormalizer.cs`
- Backend event projection no longer relies on `WorkflowEvent.ToString()` or ambiguous `Data` reflection: `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`
- CanDoItAll progress events are source-labeled and duplicate native lifecycle events are filtered: `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`

## Anti-Stub Audit

- Anti-stub transcript: `bundle://proof/SB03/transcripts/anti-stub-audit-event-normalizer.txt`
- Live external effects were not executed. SB03 proof uses in-process fake executors, API test runs, component smoke, and source assertions only.

## Downstream Smoke Proof

- Workflow API smoke: `bundle://proof/SB03/transcripts/integration-workflow-api-event-envelope-after-implementation.txt`
- Workflow component smoke: `bundle://proof/SB03/transcripts/component-workflows-page-smoke-after-event-normalizer.txt`
- Solution build: `bundle://proof/SB03/transcripts/solution-build-slnx-after-event-normalizer.txt`

## Known Residuals

- `dotnet build CanDoItAll.slnx --no-restore` still reports existing EF Core Relational `MSB3277` version-conflict warnings; it exits successfully with zero errors.
- The current in-process path intentionally consumes MAF `Run.OutgoingEvents` plus CanDoItAll progress/request capture rather than switching to a separate streaming session. SB04 checkpoint/resume work can add streaming where resume capture requires it.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Event payload envelope | `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowEventPayloads.cs` | `repo://src/CanDoItAll.Web/Api/WorkflowsApi.cs` | `bundle://proof/SB03/transcripts/unit-event-normalizer-after-implementation.txt` | `bundle://proof/SB03/transcripts/failing-first-event-normalizer-tests.txt`; `bundle://proof/SB03/transcripts/unit-event-normalizer-after-implementation.txt` |
| Event binding index | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs` | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowEventNormalizer.cs` | `bundle://proof/SB03/transcripts/unit-event-normalizer-after-implementation.txt` | `bundle://proof/SB03/transcripts/unit-event-normalizer-after-implementation.txt` |
