# 11 — ProjectStructure API Test Host Stabilization


## Problem

Codex reported ProjectStructure host lifetime replacement failures. The host bootstrap likely mixes web-host services and test service-provider overrides in a fragile way.

## Tasks

1. Audit `ProjectStructureAgentApiTestHost` and `TestApplicationBootstrap.ConfigureDefaultServices`.
2. Ensure `IHostApplicationLifetime` is registered once and intentionally.
3. Use `RemoveAll<T>`/`TryAdd` carefully where needed.
4. Separate service-provider-only bootstrap from real `WebApplication` bootstrap if necessary.
5. Add tests that create and dispose the host repeatedly without lifetime conflicts.

## Acceptance criteria

- ProjectStructure API host tests pass under Release/no-build.
- No duplicate/conflicting host lifetime registration failures.
- Host disposal cleans up ports/resources.

