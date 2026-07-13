# Codex Prompts

## Primary prompt

You are a senior C# architect working in `fyziktom/CanDoItAll` on branch `memory-providers`.

Task: perform a conservative first-stage update of Microsoft Agent Framework NuGet packages. The current branch has MAF 1.8-era package references in:

- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj`

Update only the package references needed to move the stable MAF packages to `1.13.0` and align explicit dependency-floor packages. Then fix only compile breaks caused by this update. Do not adopt new MAF features yet.

Expected package changes:

- `Microsoft.Agents.AI` `1.8.0` -> `1.13.0`
- `Microsoft.Agents.AI.OpenAI` `1.8.0` -> `1.13.0`
- `Microsoft.Agents.AI.Workflows` `1.8.0` -> `1.13.0`
- `Microsoft.Extensions.AI.Abstractions` `10.5.1` -> `10.6.0`
- `Microsoft.Extensions.DependencyInjection.Abstractions` `10.0.7` -> `10.0.9`

Do not guess preview package updates for `Microsoft.Agents.AI.A2A` or `Microsoft.Agents.AI.Mem0`. First run NuGet CLI with `--include-prerelease`; update those only if the CLI reports compatible newer packages. Otherwise keep them and fix only real restore/compile issues.

Strict constraints:

- Do not introduce `ProcessAgentRuntimeToolProvider`.
- Do not expand the `/api/processes` route set.
- Do not move process-domain behavior into MAF core.
- Do not introduce central package management unless it already exists in the branch.
- Do not suppress warnings broadly to hide real API drift.
- Do not remove approval enforcement, finalizer enforcement, provider lane gates, structured output contracts, runtime tool ownership traces, context manifests, or serialized-session compatibility checks.
- Do not perform large refactors or split classes in this package-update pass.
- All source-code comments must be in English.

Source hotspots to inspect for compile errors:

- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeAgentFactory.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeSessionBuilder.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderStreamingRunner.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/*`
- `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/**`

Execution sequence:

1. Capture baseline package/build status.
2. Apply package update only.
3. Restore.
4. Fix compile breaks with minimal adapter compatibility changes.
5. Build.
6. Run focused unit and integration tests for MAF/provider/process/workflow surfaces.
7. Update or create a concise evidence note with package versions, build/test results, skipped tests, and unresolved preview-package notes.

Validation commands:

```powershell
dotnet restore CanDoItAll.slnx
dotnet build CanDoItAll.slnx --configuration Release --no-restore
dotnet test tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~MafAgentRuntime|FullyQualifiedName~ProviderDispatchLaneGate|FullyQualifiedName~ProviderRuntimeLifecycle|FullyQualifiedName~Finalizer|FullyQualifiedName~ToolProviderComposition|FullyQualifiedName~Workflow"
dotnet test tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release --filter "FullyQualifiedName~AgentFramework|FullyQualifiedName~Process|FullyQualifiedName~ProjectStructureAgent"
rg "Microsoft\.Agents\.AI\" Version=\"1\.8\.0|Microsoft\.Agents\.AI\.OpenAI\" Version=\"1\.8\.0|Microsoft\.Agents\.AI\.Workflows\" Version=\"1\.8\.0" src tests tools -g "*.csproj"
git diff --check
```

When a test name from the prompt does not exist, find the nearest source-discovered equivalent test. Do not delete the validation intent.

Stop after the conservative update is buildable and validated. Document new MAF features for a future phase, but do not implement them now.

## Review prompt after Codex patches

Review the diff as a senior C# architect.

Check:

1. Were only the intended package references updated?
2. Were preview packages updated only based on NuGet CLI evidence?
3. Are all compile fixes inside existing MAF adapter seams?
4. Did the diff avoid new process direct tools and new process API routes?
5. Did it preserve approval gates, finalizer policy, provider lane gates, telemetry, context manifests, and session compatibility?
6. Did it avoid broad warning suppression?
7. Did it avoid central package management introduction?
8. Do build/test evidence notes match actual commands?
9. Are source comments in English?
10. Is any new feature adoption postponed to a separate next-phase note?

Reject the patch if it solves package breaks by weakening governance or moving product-domain behavior into MAF infrastructure.
