# SB03 semantic invariants

## SB03-EVENT-NODE-EXECUTOR-IDENTITY

- Invariant ID: `SB03-EVENT-NODE-EXECUTOR-IDENTITY`
- Source raw note: R4 requires typed MAF event metadata with executor and node identity.
- Expected behavior: output and executor lifecycle records include the workflow node id and configured executor id when the MAF source id maps to a workflow node binding.
- Disallowed shallow implementation: persisting only `WorkflowEvent.ToString()`, leaving node id null for known executor events, or relying on ambiguous `Data` reflection as the primary event payload path.
- Failing-first test: `bundle://proof/SB03/transcripts/failing-first-event-normalizer-tests.txt` shows the normalizer, binding index, and payload envelope were missing.
- Passing test: `bundle://proof/SB03/transcripts/unit-event-normalizer-after-implementation.txt` shows output event normalization and a multi-node workflow preserve node/executor identity.
- Production assertions: `bundle://proof/SB03/transcripts/source-assertions-event-normalizer.txt` verifies `IMafWorkflowEventNormalizer`, `MafWorkflowEventBindingIndex`, and removal of old raw-event projection patterns.

## SB03-BOUNDED-REDACTED-EVENT-PAYLOADS

- Invariant ID: `SB03-BOUNDED-REDACTED-EVENT-PAYLOADS`
- Source raw note: R6 requires event payloads to coordinate with artifact/payload policy and avoid secret leakage.
- Expected behavior: event payloads are serialized as `WorkflowEventPayloadEnvelope`, inline payloads are redacted, bounded, and annotated with source/type/node/executor/request metadata.
- Disallowed shallow implementation: writing raw executor output, raw approval responses, raw input JSON, authorization headers, tokens, passwords, or unbounded payloads directly into `WorkflowEventRecord.PayloadJson`.
- Passing test: `bundle://proof/SB03/transcripts/unit-event-normalizer-after-implementation.txt` asserts an executor output containing `raw-token-value` is persisted with `[REDACTED]` and node/executor metadata.
- Production assertions: `bundle://proof/SB03/transcripts/source-assertions-event-normalizer.txt` verifies runtime, MAF-native, CanDoItAll-progress, and external-request event sources are explicitly labeled.
- Red-team negative case: the multi-node unit workflow emits a token-like output and asserts the raw value is absent from persisted event payload JSON.

## SB03-REQUEST-TIMELINE-METADATA

- Invariant ID: `SB03-REQUEST-TIMELINE-METADATA`
- Source raw note: R2 and R4 require request state from execution-position HITL to be observable and debuggable.
- Expected behavior: waiting/request response events carry request id, request kind, and node id through the same event payload envelope contract.
- Disallowed shallow implementation: relying only on `WorkflowExternalRequestRecord` while timeline events lack the request id/kind needed by UI/API consumers.
- API proof: `bundle://proof/SB03/transcripts/integration-workflow-api-event-envelope-after-implementation.txt` shows route `api/workflows/test-runs` returns a waiting event whose envelope matches the pending request id, kind, and node id.
- Downstream dependency check: `bundle://proof/SB03/transcripts/component-workflows-page-smoke-after-event-normalizer.txt` and `bundle://proof/SB03/transcripts/solution-build-slnx-after-event-normalizer.txt` prove the workflow UI slice and solution still build.

## SB03-PROGRESS-NATIVE-EVENT-DEDUP

- Invariant ID: `SB03-PROGRESS-NATIVE-EVENT-DEDUP`
- Source raw note: SB03 requires CanDoItAll progress events and native MAF events to be deduplicated or clearly labeled.
- Expected behavior: CanDoItAll progress events use `WorkflowEventPayloadSource.CanDoItAllProgress`; native MAF events use `WorkflowEventPayloadSource.MafNative`; duplicate native executor lifecycle rows are filtered when the richer progress row exists for the same node/kind.
- Disallowed shallow implementation: emitting indistinguishable duplicate executor-completed rows that make timeline consumers choose arbitrarily.
- Passing test: `bundle://proof/SB03/transcripts/unit-event-normalizer-after-implementation.txt` includes a single completed progress event for `work-a` with the redacted executor output payload.
- Production assertions: `bundle://proof/SB03/transcripts/source-assertions-event-normalizer.txt` verifies source labels and duplicate-progress filtering are present.

- Changed source files: `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowModels.cs`, `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowEventPayloads.cs`, `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowEventNormalizer.cs`, `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`, and tests listed in `bundle://proof/SB03/manifest.md`.
- Downstream dependency check: `bundle://proof/SB03/transcripts/integration-workflow-api-event-envelope-after-implementation.txt`, `bundle://proof/SB03/transcripts/component-workflows-page-smoke-after-event-normalizer.txt`, and `bundle://proof/SB03/transcripts/solution-build-slnx-after-event-normalizer.txt` passed.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| `WorkflowEventPayloadEnvelope` | `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowEventPayloads.cs` | `repo://src/CanDoItAll.Web/Api/WorkflowsApi.cs` | `bundle://proof/SB03/transcripts/integration-workflow-api-event-envelope-after-implementation.txt` | `bundle://proof/SB03/transcripts/unit-event-normalizer-after-implementation.txt` |
| MAF source binding metadata | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs` | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowEventNormalizer.cs` | `bundle://proof/SB03/transcripts/unit-event-normalizer-after-implementation.txt` | `bundle://proof/SB03/transcripts/failing-first-event-normalizer-tests.txt` |
