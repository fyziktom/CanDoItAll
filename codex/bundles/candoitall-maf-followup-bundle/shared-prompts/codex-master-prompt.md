# Codex master prompt — MAF stabilization follow-up

You are a senior C#/.NET architect and senior Microsoft Agent Framework engineer.

The repository has already implemented a large part of the MAF stabilization work: structured output DTOs, schema response format, validators, finalizer tools, required process-step finalizer mode, tool policy middleware, provider feature matrix, and bounded repair hooks.

Your job is not to rewrite the system. Your job is to close the remaining correctness gaps and prove them with tests.

Important implementation rules:

- Keep all source-code comments in English.
- Do not perform broad unrelated refactoring.
- Do not weaken structured output validation.
- Do not parse workflow decisions from Markdown or free text.
- Do not claim test success unless the test files exist and the commands actually pass.
- Prefer small, explicit, testable changes.
- If Microsoft Agent Framework API names differ from assumptions, adapt to the installed package versions and document the actual API.

## Start with this audit

Read:

- `audit/current-state-audit.md`
- `audit/evidence-map.md`
- `requirements/requirements.md`

Then implement the subbundles in this priority order:

1. `subbundles/01-finalizer-runtime-mode-alignment/`
2. `subbundles/02-tool-policy-exception-boundary/`
3. `subbundles/03-provider-feature-consistency/`
4. `subbundles/04-hardening-test-suite-reconciliation/`
5. `subbundles/05-repair-service-contract/`
6. `subbundles/06-process-context-output-validation/`
7. `subbundles/07-tool-composition-approval-failfast/`
8. `subbundles/08-workflow-checkpoint-claims-and-roadmap/`
9. `subbundles/09-verification-document-truthfulness/`

## Expected architectural direction

### Effective execution policy before runtime build

The runtime must receive the effective structured-output/finalizer policy, not infer finalizer attachment from `structuredOutput` alone. Add an options record if needed, for example:

```csharp
public sealed record AgentRuntimeExecutionOptions(
    AgentStructuredOutputContract? StructuredOutput,
    AgentFinalizerMode FinalizerMode,
    bool RequireStructuredOutputValidation,
    int MaxStructuredOutputRepairAttempts);
```

Adapt this to the actual code style. Backward compatibility is fine, but avoid a split-brain state where MAF tools/instructions say "call finalizer" while execution validation says `Disabled`.

### Tool policy block exception

Introduce a dedicated exception such as `AgentToolPolicyBlockedException`. Throw it from policy branches only. Catch that exact type for "blocked by policy" logs/messages. Do not catch every `InvalidOperationException` from `next(...)` as a policy block.

### Provider feature consistency

Use the central feature matrix for capability flags. Avoid persisting or showing `SupportsStructuredOutput` through an outdated `Responses`-only rule. Persist selected transport in metadata instead of inferring OpenAI Chat Completions by display name.

### Tests must exist

Add the missing focused test classes or rename docs to actual tests. At minimum add:

- `AgentFinalizerPolicyTests`
- `AgentToolInvocationPolicyTests`
- `ProviderFeatureMatrixTests`
- `AgentRuntimeHardeningStaticRegressionTests`
- additional `AgentOutputContractTests` for repair and null-safety

Use reflection only when necessary. Prefer direct unit tests over brittle implementation-detail tests.

## Validation commands

Run these after implementation:

```bash
dotnet --info
dotnet restore CanDoItAll.slnx
dotnet build CanDoItAll.slnx --configuration Release --no-restore

dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~AgentFinalizerPolicyTests|FullyQualifiedName~AgentToolInvocationPolicyTests|FullyQualifiedName~ProviderFeatureMatrixTests|FullyQualifiedName~AgentRuntimeHardeningStaticRegressionTests|FullyQualifiedName~AgentOutputContractTests"

dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --configuration Release --no-build --filter "FullyQualifiedName~AgentFrameworkExecutionRunTrackingIntegrationTests|FullyQualifiedName~ProcessMockAgentRuntimeIntegrationTests|FullyQualifiedName~MafAgentRuntimeTests"
```

If a command fails, fix the failure unless it is clearly unrelated and already documented. If a command cannot be run, explain why with exact environment details.

## Deliverables

When finished, provide:

1. Audit delta: what you changed and why.
2. Files changed.
3. Tests added.
4. Commands run and exact results.
5. Remaining risks.
6. Any MAF API/version caveats.
