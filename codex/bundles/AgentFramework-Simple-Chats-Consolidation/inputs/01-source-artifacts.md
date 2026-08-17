# Source artifacts

- repo://inputs/00-original-request.md — literal user request.
- repo://src/Modules/CanDoItAll.Modules.LlmChats/ — current domain/Application/ports owner.
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/ — current EF plus provider-runtime owner.
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Ui/ — current reusable UI plus route/navigation owner.
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor — target page composition.
- repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderUsageModels.cs — current Agent usage evidence.
- repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Workspace/AgentOverviewModels.cs — current Agent dashboard projection.
- repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/ — schema compatibility authority.
- repo://tests/Solutions/ — test entrypoints.
- bundle://inputs/02-source-authority.md — authority and conflict rules.
- bundle://inventories/01-source-surface-inventory.md — inspected surface.
- bundle://analysis/02-findings-register.md — findings.
- Initial CodeAnalytics snapshot snap-20260817163454-e036fa6f.
- Follow-up UI/boundary snapshot snap-20260817172927-da2eea1a covering AgentFramework.Components, Modules.AgentFramework, and all current LlmChats projects.
- CanDoItAll.SharedInfo commit 7b7808e8591d7219f40826cf0e5624e182981d90.
