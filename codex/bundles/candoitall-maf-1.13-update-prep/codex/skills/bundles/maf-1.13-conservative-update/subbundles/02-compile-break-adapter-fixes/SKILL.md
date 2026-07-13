# Subbundle 02: Compile-Break Adapter Fixes

## Goal

Fix breaking changes caused by MAF 1.13 without changing product architecture.

## Work order

1. Restore/package graph failures.
2. Missing namespaces/types.
3. MAF agent/session/run option signatures.
4. Streaming update/content type changes.
5. Skill-source approval/caching/disposal changes.
6. FileAccess/FileMemory API changes.
7. A2A changes.
8. Workflow adapter changes.
9. Test compile fixes.

## Primary hotspots

- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeAgentFactory.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeSessionBuilder.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderStreamingRunner.cs`
- `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/**`

## Guardrails

Do not remove:

- approval policy,
- required finalizer capture,
- structured-output contracts,
- tool invocation traces,
- context manifests,
- provider dispatch gates,
- serialized session compatibility checks,
- governed process context filtering.

## Validation

```powershell
dotnet build CanDoItAll.slnx --configuration Release --no-restore
```

## Exit criteria

- Build succeeds.
- All compile fixes are minimal and localized.
- No product behavior was redesigned.
