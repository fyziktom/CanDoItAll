# 05-artifact-and-payload-policy-hardening

## Status

- Status: `Completed`

## Objective

Apply workflow artifact and payload policies consistently across run input, event payloads, executor outputs, plugin logs, and generated artifacts.

## Covered Inputs

- R6: Enforce artifact and payload policy consistently across runtime records and plugin/tool receipts.
- R4: Keep event payloads bounded and redacted.

## Prerequisites

- SB03 event records carry enough identity and payload metadata.
- SB04 checkpoint behavior does not expose raw trusted blobs.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowModels.cs`
- `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowExecutorModels.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowRuntimeManager.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExecutorContracts.cs`
- `repo://src/CanDoItAll.Modules.Plugins/Catalog/PluginLogServices.cs`
- `repo://src/CanDoItAll.AgentFramework.Persistence/CanDoItAll.AgentFramework.Persistence.csproj`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorPolicyObservabilityTests.cs`

## Scope

- Introduce a workflow payload policy service.
- Apply inline limits, redaction, and artifact capture to started input, events, executor outputs, error details, plugin logs, tool receipts, and preview outputs.
- Add artifact kinds for JSON, text, file, tool receipt, and preview simulation outputs.

## Dependency Impact

- SB06 plugin proof depends on bounded redacted plugin logs.
- SB08 final evidence must show no secret-looking raw payloads are stored in transcripts or artifacts.

## Validation Depth

- Oversized payload, redaction, and runtime artifact integration tests.
- Critical proof requires adversarial secret/oversized negative cases.

## Implementation Steps

1. Introduce `IWorkflowPayloadPolicyService`.
2. Apply `WorkflowSettings.ArtifactPolicy.MaxInlinePayloadCharacters` across runtime input, output, event, error, executor, plugin-log, and tool-receipt paths.
3. Create JSON/text/tool receipt artifacts when inline limits or capture policy require them.
4. Redact before storage and artifact creation.
5. Add artifact kind mapping for runtime and preview payload types.
6. Ensure event records can reference artifact ids/paths without breaking consumers.

## Do Not Do

- Do not silently drop payloads without summary/reference.
- Do not store secrets in artifact payloads.
- Do not create unlimited artifact counts for large fan-out workflows.

## Acceptance Checklist

- Large input/output payloads are not stored inline beyond policy.
- Artifact records are created when output capture is enabled.
- Plugin logs are redacted and bounded.
- UI/API tests still show meaningful summaries.

## Proof Required

- Oversized payload unit tests.
- Redaction tests.
- Runtime artifact integration test.
- `bundle://proof/SB05/manifest.md` and `bundle://proof/SB05/semantic-invariants.md`.

## Browser Validation Logging

- Browser proof is required only if artifact display UI changes.

## Progression Gate

- Continue to SB06 only after plugin/runtime payload paths enforce bounds and redaction consistently.

Result: `Passed`. Runtime input/output/event/request/error paths, plugin logs, and tool receipts now use bounded redacted payload policy handling. See `bundle://proof/SB05/manifest.md`.

## Suggested Agent Prompt

Implement one policy service for workflow payload storage, redaction, inline bounds, and artifact references across runtime and plugin executor paths.
