# C# Testability Plan

## Characterization Tests

Run before and after package changes where feasible:

- `dotnet test tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~MafPackageBaselineReflectionTests|FullyQualifiedName~MafRuntimeArchitectureServicesTests|FullyQualifiedName~MafAgentRuntimeToolProviderCompositionTests"`
- `dotnet test tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release --filter "FullyQualifiedName~AgentFrameworkExecutionRunTrackingIntegrationTests|FullyQualifiedName~MafAgentRuntimeHandoffTests|FullyQualifiedName~ProjectStructureAgentIntegrationTests"`

If exact filters fail due test name changes, discover nearest tests and document the replacement.

## Isolated Unit Tests

Required for existing behavior:

- Provider lane gate and provider runtime lifecycle tests.
- Finalizer policy tests.
- MAF tool provider composition tests.
- Workflow adapter isolation and event normalizer tests.
- Process dispatch application service tests.

Required if new compatibility helpers are introduced:

- Direct unit test for the helper without constructing `MafAgentRuntime`.
- Unit test for unsupported or malformed input.
- Unit test for package API mapping preserving existing domain output.

## Negative Tests

At least one focused negative proof is required for critical behavior touched by implementation:

- Unsafe mutation tool still requires approval.
- Missing required finalizer is not accepted as successful process output.
- Unsupported provider/session/preview package condition fails explicitly.
- Mem0 not-found package decision is not hidden by silent fallback.
- Process direct tool names are not introduced as production capabilities.

## Integration And Composition Smoke Tests

- `dotnet restore CanDoItAll.slnx`
- `dotnet build CanDoItAll.slnx --configuration Release --no-restore`
- Focused integration tests for agent framework execution and project structure bridge.
- Component tests if UI-adjacent packages or components compile against changed dependencies.
- Playwright smoke only if environment is ready; otherwise record exact skip reason.

## Fake Provider Tool Driver Proof

When provider/tool/workflow behavior is touched, tests must use fake providers or fake workflow components for unit-level behavior. Live external credentials belong only in explicit integration tests and must not be required for unit proof.

## Exit Criteria

- Tests prove behavior, not merely non-null descriptors or command success.
- Any extracted behavior has direct tests that do not construct `MafAgentRuntime`.
- A composition smoke proves DI or registration changes if any were made.
- Source assertions show governance behavior still lives on the production path.
