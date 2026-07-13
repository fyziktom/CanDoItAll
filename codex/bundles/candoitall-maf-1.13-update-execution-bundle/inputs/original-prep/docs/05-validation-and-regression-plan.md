# Validation and Regression Plan

## Required local environment

- .NET 10 SDK
- Windows PowerShell
- PostgreSQL profile for governed process-agent automation where available
- Qdrant only for memory/vector scenarios
- Playwright Chromium installed only for Playwright test pass

## Validation layers

| Layer | Purpose | Required in phase 1 |
| --- | --- | --- |
| Restore | Prove package graph is consistent. | Yes |
| Build | Prove adapter compiles against MAF 1.13. | Yes |
| Focused unit tests | Prove MAF adapter seams, provider gates, finalizers, approvals. | Yes |
| Focused integration tests | Prove AgentFramework/process/project-structure integration. | Yes |
| Component tests | Prove UI component compile/runtime assumptions where relevant. | Recommended |
| Playwright | Prove user-visible smoke behavior. | Optional if environment is ready |
| PostgreSQL process smoke | Prove governed process automation can still launch/dispatch. | Recommended |
| Qdrant/memory smoke | Prove memory branch still compiles and basic memory flow survives. | Recommended when services are available |

## Commands

### Restore and build

```powershell
dotnet restore CanDoItAll.slnx
dotnet build CanDoItAll.slnx --configuration Release --no-restore
```

### Focused tests

```powershell
dotnet test tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~ProcessRuntimeDispatchApplicationServiceTests|FullyQualifiedName~ProviderDispatchLaneGateTests|FullyQualifiedName~ProviderRuntimeLifecycleTests|FullyQualifiedName~AgentProviderFailureDisplayFormatterTests|FullyQualifiedName~MafAgentRuntimeToolProviderCompositionTests"
dotnet test tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release --filter "FullyQualifiedName~ProjectStructureAgentIntegrationTests|FullyQualifiedName~AgentFrameworkExecutionRunTrackingIntegrationTests"
```

If those exact test names do not exist in the current branch, do not delete this intent. Replace the command with the nearest source-discovered tests and document the replacement.

### Broad tests

```powershell
dotnet test tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration Release
dotnet test tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release
dotnet test tests\Components\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --configuration Release
```

### Playwright smoke

```powershell
dotnet test tests\Playwright\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --configuration Release
```

## Source scans

Run these after fixes:

```powershell
rg "registers .*ProcessAgentRuntimeToolProvider|Current direct runtime tools: 23|/api/processes/definitions|/api/processes/templates|/api/processes/runs/\{runId\}/detail|ProcessManagerTools" docs src/MAF/Common/CanDoItAll.AgentFramework.Core src/MAF/Common/CanDoItAll.AgentFramework.Maf -g "*.md" -g "!processes-maf-providers-implementation-map.md"
rg "Microsoft\.Agents\.AI" Version="1\.8\.0|Microsoft\.Agents\.AI\.OpenAI" Version="1\.8\.0|Microsoft\.Agents\.AI\.Workflows" Version="1\.8\.0" src tests tools -g "*.csproj"
rg "Microsoft\.Extensions\.AI\.Abstractions" Version="10\.5\.1|Microsoft\.Extensions\.DependencyInjection\.Abstractions" Version="10\.0\.7" src tests tools -g "*.csproj"
git diff --check
```

## Behavioral regression checklist

### Agent runtime

- Standard chat still returns non-empty text.
- Streaming updates are captured and not double-counted.
- Token usage is recorded when provider supplies usage.
- Provider failure messages are redacted and user-actionable.
- Temperature retry still works for transports/models that reject explicit temperature.
- Input attachment vision fallback still selects a vision-capable model.

### Tools and approvals

- Read tools remain callable.
- Mutation tools still require approval or an equivalent governed approval path.
- Providers without effective MAF approval support do not expose unsafe mutation tools during governed process automation.
- Pending approvals serialize/restore only when compatible.
- Approval continuation does not replay stale or unrelated tool calls.

### Structured output and finalizers

- Required finalizer tools are attached for process-step contracts.
- A required finalizer remains the authoritative process-step output.
- Missing required finalizer repair remains bounded.
- Typed JSON fallback is used only as existing repair behavior, not as a primary replacement.
- Finalizer traces and tool invocation traces remain available for process evidence.

### Processes

- `/api/processes/launch/check` still reports readiness based on actual agent/provider state.
- `/api/processes/launch` still creates durable run state.
- `/api/processes/runs/{runId}/dispatch` still dispatches ready steps.
- Cancel, rework, live, detail, and history routes remain intact.
- Project-structure process bridge tools still link/start/subprocess-launch as before.
- No new direct process runtime tool provider appears.

### Workflows

- Existing MAF workflow adapter tests compile and pass.
- Existing handoff runtime build path still composes participant agents.
- Existing workflow run/checkpoint semantics are preserved; do not opt into new durability features.

### Memory branch

- Memory projects compile.
- Mem0 package usage is either compatible or explicitly isolated behind existing feature gates.
- Memory provider abstractions are not changed to chase MAF package APIs in phase 1.
