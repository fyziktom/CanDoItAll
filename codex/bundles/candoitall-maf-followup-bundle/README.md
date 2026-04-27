# CanDoItAll MAF stabilization follow-up bundle — 2026-04-27

This bundle is a follow-up audit and implementation plan for the updated CanDoItAll agent integration after Codex completed the previous MAF stabilization work.

Overall status: the repository is materially improved, but not finished. The current code now contains strong structured-output primitives, required finalizer mode on the main process automation path, transcript finalization before assistant-message persistence, finalizers for all listed critical DTOs, repair hooks, provider feature gates, and tool policy middleware. The remaining work is mostly about making those mechanisms internally consistent, fail-safe, and test-proven.

## Highest-priority gaps

1. Runtime finalizer tooling/instructions are attached based on `StructuredOutput` alone, while enforcement mode is resolved later from execution metadata. This can instruct the model to call a finalizer that the execution service later treats as `Disabled` or `Shadow`.
2. The MAF function-call middleware wraps any downstream `InvalidOperationException` or `NotSupportedException` as "blocked by policy", which can mislabel real tool failures as policy decisions.
3. The workspace-backed provider registry still persists `SupportsStructuredOutput = model.Transport == Responses`, which conflicts with the central feature matrix where compatible OpenAI/Azure Chat Completions profiles can support JSON schema response format.
4. The verification document claims focused hardening tests exist and passed, but the uploaded repository ZIP does not contain the named unit test files.
5. The default repair service is a conservative JSON-object extractor, not a semantic repair agent. That may be fine, but the architecture and tests must describe it truthfully and leave a clean seam for semantic repair if desired.
6. Process-step outcome validation is still mostly generic; contextual validation of branch outcome keys, required evidence, and contract strictness belongs closer to the dispatcher/runtime boundary.

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

Do not claim completion unless the actual repository contains the test classes referenced by the verification documentation and the commands above pass in the target environment.
