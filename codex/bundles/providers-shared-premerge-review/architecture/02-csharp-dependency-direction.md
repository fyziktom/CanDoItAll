# C# Dependency Direction

Both recorded scoped snapshots have no detected cycles. This is scoped evidence, not full graph certification.

Inspected references:
- History.Application -> History.Abstractions.
- History.Persistence -> History.Abstractions, History.Application, Foundation.Infrastructure.
- SharedProviders.Http -> SharedProviders.Abstractions, AgentFramework.Models, AgentFramework.Providers.
- ProviderManagement -> Core, Models, Providers, Infrastructure, SharedKernel, Security, SharedProviders.Abstractions, History.Abstractions, History.Persistence.
- Web/Composition owns adapters and wiring, not neutral history contracts.

Target references unchanged. No new contract project needed. Forbidden: Abstractions -> Web/Persistence/provider SDK; core policy -> UI; SharedInfo runtime dependency from product; cycles hidden by moving implementations into Common.

Before/after proof at architecture checkpoints: scoped CodeAnalytics, direct .csproj diff, build affected projects, isolated policy tests and a production-composition smoke. Any added reference reopens this map and requires a whole affected graph cycle check before feature work continues.
