# C# Dependency Direction

## Allowed References

- Projections may depend on Contracts/Abstractions value types already allowed by its project graph.
- Application may depend on Projections, Runtime, and Abstractions.
- Persistence may implement Projections/Application-facing storage contracts and map runtime identifiers.
- Modules.Processes may adapt Agent Framework services to Application/Projections contracts.
- Web and Workbench may consume Application query services.

## Forbidden References

- Runtime -> Projections/Application/Persistence/Modules/Web.
- Projections -> EF Core, Agent Framework, Web DTOs, or module services.
- Persistence -> provider-specific LLM/Agent Framework policy.
- Web API -> `DbContext`, file workspace store, or runtime unit of work for normal history.
- Project-structure contributor -> Agent Framework detail hydration.

## Review Checks

1. Inspect changed `.csproj` references.
2. Search new types for forbidden namespaces.
3. Verify API methods inject application query/finalization abstractions rather than stores.
4. Verify EF configuration has no navigation/cascade relationship for ID references.
5. Compile the affected project chain before the full solution.
