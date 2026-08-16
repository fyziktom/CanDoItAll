# Dependency direction

## Allowed project references

- `CanDoItAll.AgentFramework.Components` → `CanDoItAll.Conversations.Components`
- `CanDoItAll.Modules.AgentFramework` → existing Agent projects and the neutral project as needed
- `CanDoItAll.Tests.Components` → neutral project and existing component projects

## Forbidden references from the neutral project

- any `src/MAF/**` project
- any `src/Modules/**` project
- `CanDoItAll.Infrastructure`
- `CanDoItAll.Web`
- `CanDoItAll.Composition`
- EF Core
- provider driver or SDK projects
- persistence projects
- Process runtime projects

## Forbidden source imports in the neutral project

- `CanDoItAll.AgentFramework.*`
- `CanDoItAll.Modules.AgentFramework.*`
- `CanDoItAll.Modules.LlmChats.*`
- `Microsoft.EntityFrameworkCore`
- `AppDbContext`
- `IServiceProvider` for service location
- runtime coordinator/service interfaces

## Cycle proof

Before SB02 changes and after SB02/SB08:

1. build a scoped CodeAnalytics snapshot;
2. verify dashboard health;
3. record project inventory;
4. run dependency/cycle analysis;
5. record direct and reverse references;
6. fail CP1/CP4 on any cycle or wrong inward reference.

Do not infer graph safety only from a successful build.
