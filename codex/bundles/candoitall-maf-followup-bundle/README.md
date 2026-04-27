# CanDoItAll MAF stabilization follow-up bundle — 2026-04-27

This bundle is a follow-up audit and implementation plan for the updated CanDoItAll agent integration after Codex completed the previous MAF stabilization work.

Overall status: completed for the scoped follow-up bundle. The repository now contains strong structured-output primitives, required finalizer mode on the main process automation path, transcript finalization before assistant-message persistence, finalizers for all listed critical DTOs, repair hooks, provider feature gates, and tool policy middleware. This bundle aligned the runtime with effective finalizer modes, separated policy blocks from tool failures, centralized provider capability persistence, made repair semantics explicit, added process-context validation, fail-fast approval-tool composition, truthful workflow-checkpoint documentation, and focused hardening proof.

## Closed gaps

1. Runtime finalizer tooling/instructions now use the effective `AgentFinalizerMode` carried by `AgentRuntimeExecutionOptions`.
2. MAF tool policy blocks now use `AgentToolPolicyBlockedException` and no longer catch broad downstream tool exceptions as policy blocks.
3. Workspace-backed provider persistence now uses the central feature matrix and persists explicit provider transport metadata.
4. The hardening verification document names real test classes and records the actual focused command results.
5. The default repair path is explicitly documented and tested as conservative JSON-object extraction through `JsonObjectExtractionAgentOutputRepairService`.
6. Process-step outcome validation now has dispatcher-level context checks for branch selection, evidence references, and governed completion gaps.

## How to use this bundle

Use `shared-prompts/codex-master-prompt.md` as the main Codex instruction. Each subbundle is intentionally scoped and testable. Implement in priority order unless a build failure requires a different sequence.

Expected baseline commands after implementation:

```bash
dotnet --info
dotnet restore CanDoItAll.slnx
dotnet build CanDoItAll.slnx --configuration Release --no-restore

dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~AgentFinalizerPolicyTests|FullyQualifiedName~AgentToolInvocationPolicyTests|FullyQualifiedName~ProviderFeatureMatrixTests|FullyQualifiedName~AgentRuntimeHardeningStaticRegressionTests|FullyQualifiedName~AgentOutputContractTests"
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --configuration Release --no-build --filter "FullyQualifiedName~AgentFrameworkExecutionRunTrackingIntegrationTests|FullyQualifiedName~ProcessMockAgentRuntimeIntegrationTests|FullyQualifiedName~MafAgentRuntimeTests"
```

The commands above pass in this target environment as recorded in `docs/agent-runtime-hardening-verification.md`.
