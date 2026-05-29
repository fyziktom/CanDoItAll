# 05-artifact-and-payload-policy-hardening

## Objective

Apply workflow artifact and payload policies consistently across run input, event payloads, executor outputs, plugin logs, and generated artifacts.

## Current problem

The backend currently creates configured file artifacts only after successful completion for selected file/spreadsheet executor settings. Started events can store raw input. Executor outputs and native MAF event payloads are not consistently split into artifacts when too large.

## Exact source references

- `src/CanDoItAll.AgentFramework.Models/Workflows/*Artifact*`
- `src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowRuntimeManager.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`
- `src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExecutorContracts.cs`
- `src/CanDoItAll.Modules.Plugins/Catalog/PluginLogServices.cs`
- `src/CanDoItAll.AgentFramework.Persistence/*`
- `tests/CanDoItAll.Tests.Unit/WorkflowExecutorPolicyObservabilityTests.cs`

## Implementation steps

1. Introduce `IWorkflowPayloadPolicyService`.
2. Apply `WorkflowSettings.ArtifactPolicy.MaxInlinePayloadCharacters` to:
   - started input payloads,
   - output events,
   - executor completed payloads,
   - error details,
   - plugin log details,
   - tool receipts.
3. Create JSON/text/tool receipt artifacts when payloads exceed inline limits or when capture policy requires artifacts.
4. Redact before storage and before artifact creation where secrets may appear.
5. Add artifact kind mapping for:
   - JSON node outputs,
   - text outputs,
   - file outputs,
   - tool receipts,
   - preview simulation outputs.
6. Ensure event records can reference artifact ids/paths without breaking existing consumers.

## Do not do

- Do not silently drop payloads without a summary/reference.
- Do not store secrets in artifact payloads.
- Do not create unlimited artifact counts for large fan-out workflows.

## Acceptance checklist

- Large input/output payloads are not stored inline beyond policy.
- Artifact records are created when output capture is enabled.
- Plugin logs are redacted and bounded.
- UI/API tests still show meaningful summaries.

## Proof required

- Oversized payload unit tests.
- Redaction tests.
- Runtime artifact integration test.
