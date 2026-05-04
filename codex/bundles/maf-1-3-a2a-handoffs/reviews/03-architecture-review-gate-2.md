# Architecture Review Gate 2

## Decision

- `Proceed` to subbundle 11 validation and operator proof.

## Findings

- Process integration uses CanDoItAll-owned typed metadata (`AgentProcessCooperationMetadata`, `AgentProcessCooperationMode`, and `AgentWorkspaceToolProfileKind`) rather than preview A2A SDK types.
- `CanDoItAll.Modules.Processes` does not reference MAF preview SDK types or construct A2A clients. It only resolves process-owned cooperation intent and selected agent configuration.
- Core execution owns invocation metadata, trusted process-run parsing, and execution-log projection. This keeps process dispatch from reaching into Maf internals.
- Maf owns the runtime effect of trusted process workspace-tool profile overrides through `WorkspaceExecutionAuditContext`; overrides are process-scoped and keep configured external target/storage boundaries.
- Process prompts now expose the cooperation plan and explicitly forbid hidden background collaboration. This preserves operator visibility.
- The deterministic three-agent process proof still enforces upstream artifact stat/read inspection by QA and now verifies process cooperation metadata, runtime audit state, and execution logs.

## Accepted Risks

- Process role profile selection is inferred from role, step, work-brief, artifact, and selected-agent configuration text. This is acceptable for this bundle because the inference is centralized and tested, but an explicit process-editor override should be added if operators need pinned non-obvious profiles.
- Current process integration does not force every template to use MAF local handoff or A2A. Cooperation remains opt-in through agent configuration and process artifact handoff, which is safer than silently adding hidden agent calls.
- Existing NU1902 and NU1904 advisory warnings remain outside this gate.

## Proof Reviewed

- `dotnet build src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj --no-restore -m:1`: passed with existing warnings.
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessMockAgentRuntimeIntegrationTests" --no-restore -m:1`: passed; 7 tests.
- `git diff --check`: passed with existing LF-to-CRLF warnings only.
- Source grep showed no preview A2A SDK references in `CanDoItAll.Modules.Processes`; Core references MAF Workflows only for the stable workflow/checkpoint contracts.

## Validation Scope Update

Subbundle 11 should run process dispatch, process mock, Maf runtime, tool-profile, and seed tests. Broad validation should keep the existing advisory warnings recorded rather than treating them as failures for this bundle.
