# Readiness gate

Do not mark the MAF stabilization follow-up complete until all applicable gates are satisfied.

## Code gates

- [ ] Runtime finalizer attachment uses effective finalizer mode, not structured output alone.
- [ ] Disabled mode attaches no finalizer tool and appends no finalizer instruction.
- [ ] Required and shadow modes have instructions compatible with JSON schema `ResponseFormat`.
- [ ] Required finalizer exact-one validation still fails missing/duplicate/malformed/invalid output.
- [ ] Required finalizer output replaces `ResponseText` before assistant transcript persistence.
- [ ] Tool policy block uses a dedicated exception type.
- [ ] Real downstream tool failures are not mislabeled as policy blocks.
- [ ] Provider registry capability flags match the central provider feature matrix.
- [ ] Provider transport round-trips through explicit metadata/settings.
- [ ] Repair service behavior is accurately named/documented/tested.
- [ ] Process-context output validation is explicit and tested.
- [ ] Unusable mutation tools are not silently exposed to the model.
- [ ] Workflow/checkpoint docs distinguish checkpoint bridging from full workflow orchestration.

## Test gates

Run:

```bash
dotnet --info
dotnet restore CanDoItAll.slnx
dotnet build CanDoItAll.slnx --configuration Release --no-restore

dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~AgentFinalizerPolicyTests|FullyQualifiedName~AgentToolInvocationPolicyTests|FullyQualifiedName~ProviderFeatureMatrixTests|FullyQualifiedName~AgentRuntimeHardeningStaticRegressionTests|FullyQualifiedName~AgentOutputContractTests"

dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --configuration Release --no-build --filter "FullyQualifiedName~AgentFrameworkExecutionRunTrackingIntegrationTests|FullyQualifiedName~ProcessMockAgentRuntimeIntegrationTests|FullyQualifiedName~MafAgentRuntimeTests"
```

Acceptance:

- Build has 0 errors.
- Focused unit test filter discovers the intended hardening tests and passes.
- Focused integration tests pass.
- Verification docs list only tests that exist in the repository.

## Documentation gates

- [ ] `docs/agent-output-contracts.md` accurately describes repair and finalizer source-of-truth behavior.
- [ ] `docs/maf-runtime-stabilization.md` accurately describes provider gates and workflow/checkpoint state.
- [ ] `docs/agent-runtime-hardening-verification.md` contains only real commands/results.
- [ ] Known failures are not hidden or described as success.
