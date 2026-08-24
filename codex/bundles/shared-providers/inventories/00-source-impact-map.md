# Source impact map

This map is directional. Codex must refresh it before each subbundle.

## New preferred production paths

| Path | Intended role | First owner |
| --- | --- | --- |
| `src/Integration/CanDoItAll.SharedProviders.Abstractions/` | SDK/EF/Web-free protocol and ports | SB01 |
| `src/Integration/CanDoItAll.SharedProviders.Http/` | source client and central relay adapters | SB04/SB05 |
| `src/Modules/CanDoItAll.Modules.Workspace/SharedProviders/` | entities, EF configs, application services, reconciliation | SB02 |
| `src/App/CanDoItAll.Web/Api/SharedProvidersApi.cs` | native catalog and compatibility route mapping | SB03/SB04 |
| `src/App/CanDoItAll.Web/Api/AccessContext*` or current middleware folder | scoped header binding | SB01 |
| `tests/Support/CanDoItAll.SharedProviders.TestUpstream/` | deterministic upstream service | SB07 |
| `tools/SharedProviders/` | E2E orchestration and run scripts | SB07/SB10 |
| `docs/shared-providers.md` | architecture/user overview | SB10 |
| `docs/runbooks/shared-providers.md` | operator runbook | SB10 |
| `compose.shared-providers.e2e.yaml` | three-app proof topology | SB07 |

Names may follow current conventions; ownership may not drift.

## Existing likely modified paths

### Provider model/Workspace

- `src/Modules/CanDoItAll.Modules.Workspace/CanDoItAll.Modules.Workspace.csproj`
- `src/Modules/CanDoItAll.Modules.Workspace/Services/WorkspaceModuleServiceCollectionExtensions.cs`
- provider deletion/reference policy files discovered in SB00
- `ProviderManagementPanel.razor` and code-behind/child components

### AgentFramework projection

- `src/Modules/CanDoItAll.Modules.AgentFramework/Providers/WorkspaceAgentProviderProfileMapper.cs`
- `WorkspaceBackedAgentProviderProfileRegistry.cs` or a new outer materializer beside it
- `AgentFrameworkProviderMetadata.cs`
- module registration tests

### Web/API

- `src/App/CanDoItAll.Web/Api/ApiEndpointRouteBuilderExtensions.cs`
- `ApiAuthorizationPolicies.cs`
- `ApiServiceCollectionExtensions.cs`
- current API error/OpenAPI helpers
- Web/Composition project references and registration

### API access

- `src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiAccessScopeNames.cs`
- token default/example scope UI as appropriate

### Persistence

- new module entity configuration files
- `src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/<new migration>`
- `AppDbContextModelSnapshot.cs`
- design-time registration only if current model registry requires it

### Usage

- smallest existing ProviderUsage contracts/projection/UI surfaces needed to represent relay
  truthfully; do not broaden before SB00 proof

### Tests

- focused Unit/Components/Integration/Playwright test classes
- solution inventory only when new projects require it
- architecture guardrail tests

### Documentation/SharedInfo

- `docs/testing.md`
- root or docs indexes
- `.gitignore`
- `_candoitall-api-shared` snapshot/manifest/README
- new `codex/skills/candoitall-api-shared-providers/`
- SharedInfo validation/parity manifests

## Do-not-touch by default

- unrelated agent/workflow/process behavior;
- generic provider model shapes unless a narrow effective-projection need is proven;
- MAF SDK wrappers beyond the minimum shared projection compatibility;
- existing public simple chat/agent APIs;
- CI/CD;
- production database provider selection.
