# Microsoft Learn Grounding

## Sources

- `https://learn.microsoft.com/dotnet/core/extensions/dependency-injection/service-registration`
- `https://learn.microsoft.com/dotnet/core/extensions/dependency-injection/guidelines`
- `https://learn.microsoft.com/dotnet/core/extensions/options`
- `https://learn.microsoft.com/dotnet/core/extensions/options-library-authors`

## Architecture Implications

| Guidance | Bundle implication |
| --- | --- |
| Register related services with grouped `Add{GROUP_NAME}` extension methods. | Use `AddCanDoItAllMafRuntime`, `AddCanDoItAllMafCapabilityComposition`, `AddCanDoItAllMafProviderRuntime`, and similar focused registrations instead of deep fallback construction. |
| Multiple implementations are resolved through `IEnumerable<T>` in registration order. | Keep runtime tool providers and context contributors as extension points, but compose them through a testable composer with deterministic ordering and metadata validation. |
| Use duplicate-safe registration for library-provided implementations. | Use `TryAdd`/`TryAddEnumerable` style registration for default runtime collaborators and extension providers. |
| Services should be small, well-factored, and easy to test; many dependencies can signal SRP violation. | `MafAgentRuntime` should not be the only place to test capability composition, provider build, workspace tools, MCP, context, and finalization. |
| Avoid service locator when constructor injection works. | Extracted drivers should receive dependencies in constructors or typed factories, not resolve them from raw `IServiceProvider` during execution. |
| Keep DI factories fast and synchronous. | Runtime startup should not run expensive provider/tool work in registration factories; composition measurements belong in runtime execution. |
| Use strongly typed options with validation. | Runtime fallback policy, measurement options, context/tool limits, and provider composition limits should be options classes with validation. |

## Boundaries This Does Not Justify

Microsoft Learn guidance does not require a third-party container, broad assembly scanning, or a class-per-method refactor. The built-in DI container and explicit grouped registrations are enough if the collaborators have real responsibility boundaries and tests.
