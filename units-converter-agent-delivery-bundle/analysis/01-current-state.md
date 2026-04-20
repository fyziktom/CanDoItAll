# Current State

## Agent Ownership Split

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\AgentFrameworkWorkspaceFactory.cs` resolves the organization scope from the active database-profile id and feeds that scope into the dedicated Agents page.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentCatalogPanel.razor.cs` lists technical agents directly from that organization workspace service.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\CrmHrServices.cs` builds the CRM-HR AI directory from CRM party rows plus the AgentFramework technical-agent bridge.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\AgentFrameworkAiTechnicalAgentBridge.cs` reads only the organization workspace selected by the AgentFramework workspace factory, but the target profile already contains stale CRM bindings that reference a larger legacy organization catalog.

## Evidence From The Target Profile

- Target DB: `C:\Users\lucys\AppData\Local\CanDoItAll\control-plane\database-profiles\managed-sqlite\529c12060808489fad29feb5bc60dda1\db\candoitall.db`
- `CrmHr_Parties` currently contains `14` AI-agent parties for the target profile.
- `CrmHr_AiResourceBindings` currently contains `14` AI-resource bindings, all marked as AgentFramework projections.
- `Showcase Lead Engineer` exists as a CRM AI-agent party and binding in that database.
- The target workspace root contains two organization-scope catalogs:
  - `C:\Users\lucys\AppData\Local\CanDoItAll\control-plane\database-profiles\managed-sqlite\529c12060808489fad29feb5bc60dda1\workspace\data\scopes\organization\2519a3e7d8d4c6711130ae17a93d6b2a\workspace.json`
  - `C:\Users\lucys\AppData\Local\CanDoItAll\control-plane\database-profiles\managed-sqlite\529c12060808489fad29feb5bc60dda1\workspace\data\scopes\organization\529c12060808489fad29feb5bc60dda1\workspace.json`
- The legacy `2519...` scope contains the showcase-specific agents that the user still sees through CRM-HR.
- The active profile-id `529c...` scope contains only the current baseline catalog and is what the dedicated Agents page renders.

## Playwright And Capability State

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\SandboxWorkspaceSeedBuilder.cs` already seeds `playwright-local-mcp` for baseline UI delivery agents.
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs` and `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests.cs` already cover seeded capability presence and provider-native MCP execution evidence.
- The missing proof is not basic registration anymore; it is serious-project readiness under OpenAI-backed delivery roles, with the right instructions, capabilities, and screenshot-analysis expectations for the real units-converter flow.

## Process And Scenario State

- The current high-fidelity end-to-end path is still anchored in `C:\repositories\CanDoItAll\tools\CanDoItAll.ScenarioSeeder\AgentShowcaseCalculatorSeeder.cs` and `C:\repositories\CanDoItAll\tools\CanDoItAll.ScenarioSeeder\AgentShowcaseCalculatorSeeder.Workflow.cs`.
- Those files still encode showcase naming, showcase-specific roles, showcase artifact paths, and showcase narrative language, which conflicts with the user’s new request for a serious project.
- The template library already exists under `C:\repositories\CanDoItAll\Templates\Processes`, but the current serious-delivery path still appears to rely partly on scenario-specific composition rather than purely reusable templates.

## Architecture And Refactor Pressure

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\CrmHrServices.cs` is currently `4585` lines and remains a likely refactor hotspot.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureAssemblyService.cs` is currently `1246` lines and sits on the cross-module projection boundary for process-run visibility.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\SandboxWorkspaceSeedBuilder.cs` is currently `858` lines and owns seeded capability and baseline-agent composition.
- `C:\repositories\CanDoItAll\tools\CanDoItAll.ScenarioSeeder\AgentShowcaseCalculatorSeeder.Workflow.cs` is currently `1388` lines and carries too much scenario-specific orchestration logic.

## Immediate Conclusion

- The first critical defect is not a display bug. It is a canonical-source split inside the AgentFramework workspace storage layout.
- The second critical defect is process and agent hardening drift: serious delivery still leans on showcase-flavored scenario content instead of template-driven, reusable serious-project composition.
