# SB05 semantic invariants

Status: Completed

## SB05-PAYLOAD-ARTIFACT-POLICY

- Invariant ID: `SB05-PAYLOAD-ARTIFACT-POLICY`
- Source raw note: R6 requires artifact and payload policy to be enforced consistently across runtime records, plugin logs, and tool receipts.
- Expected behavior: all workflow payload storage paths redact before bounding, create artifact metadata when policy requires capture, and avoid raw secret leakage.
- Disallowed shallow implementation: truncating only started events, skipping plugin/tool receipts, or creating artifact references that store raw unredacted blobs in normal workflow records.
- Failing-first test: `bundle://proof/SB05/transcripts/failing-first-payload-policy-tests.txt` shows policy service and artifact kind support were missing.
- Passing test: `bundle://proof/SB05/transcripts/unit-payload-policy-after-implementation.txt` and `bundle://proof/SB05/transcripts/integration-payload-policy-after-implementation.txt` passed.
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowPayloadPolicyService.cs`, `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`, `repo://src/CanDoItAll.Modules.Plugins/Catalog/PluginLogServices.cs`, and tests listed in `bundle://proof/SB05/manifest.md`.
- Production assertions: `bundle://proof/SB05/transcripts/source-assertions-payload-policy.txt` verifies policy service, runtime call sites, plugin log call sites, artifact kinds, and regression tests.
- Red-team negative case: oversized and token-like payload tests assert bounded output and redaction before persistence.
- Downstream dependency check: `bundle://proof/SB05/transcripts/build-after-sb05.txt` and `bundle://proof/SB05/transcripts/integration-payload-policy-after-implementation.txt` passed.

## Payload Policy

- All runtime payload storage paths must redact before bounding and before creating artifact metadata.
- Inline runtime/event payload text must never exceed the effective `WorkflowArtifactPolicy.MaxInlinePayloadCharacters` value.
- Invalid inline payload limits fail predictably instead of silently falling back to an unbounded value.
- Artifact records created by the payload policy are metadata/reference records only; they must not persist raw unredacted payload blobs in normal workflow records.
- Artifact records are created only when a run id is available, artifact capture is requested by truncation or output-capture policy, and the artifact kind is allowed by policy.

## Runtime Events

- Started events use the payload policy for run input and include truncation/reference metadata when the inline input is bounded.
- Completed node progress uses `Json`, `Text`, or `PreviewSimulation` artifact kind based on output shape and node simulation state.
- Failed node progress uses the executor-error scope and stores a bounded/redacted error payload.
- External request payloads are bounded/redacted before waiting-state records, event envelopes, or checkpoint records reference them.

## Plugins And Tools

- Plugin log messages are capped at the log-message limit and details are capped at the details limit after redaction.
- Plugin logs without a workflow run id remain inline-only, bounded, and redacted; they do not fabricate artifact records.
- Workspace tool receipts are redacted and bounded before run detail persistence.
- `WorkflowSettings.Default.ArtifactPolicy.AllowedArtifactKinds` includes `ToolReceipt` and `PreviewSimulation`.

## Residual Boundary

- The file sandbox execution-slice store does not have access to workflow settings, so tool receipt bounding uses the shared workflow event inline limit.
- Durable blob storage for full redacted payload artifacts remains future backend work; current records expose safe references and summaries only.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Payload policy result | `WorkflowPayloadPolicyService` | runtime/backend/plugin/tool receipt stores | Produced before inline storage and artifact metadata creation. | `bundle://proof/SB05/transcripts/unit-payload-policy-after-implementation.txt` |
| Safe artifact metadata | runtime backend | workflow API/run detail consumers | Created only when capture/truncation policy requires it. | `bundle://proof/SB05/transcripts/integration-payload-policy-after-implementation.txt` |
