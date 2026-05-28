# Source Artifacts Reviewed While Preparing This Bundle

## Repository snapshot signals

- Repository: `fyziktom/CanDoItAll`
- Branch requested by user: `processes-hardening`
- GitHub compare against `main` resolved successfully, so the branch exists even though branch search did not return it by name.
- Compare status observed: branch diverged from `main`; the branch was hundreds of commits ahead and one commit behind at preparation time.

## Repository files inspected

- `CanDoItAll.slnx`
  - Confirms the solution includes `CanDoItAll.AgentFramework.*`, `CanDoItAll.Modules.AgentFramework`, `CanDoItAll.Modules.Plugins`, and plugin projects for Docker, Email, Gmail, and Office365.
- `src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
  - Confirms MAF packages are referenced from the dedicated MAF integration project.
- `Templates/Workflows/manifest.yaml`
  - Confirms workflow templates are repository-owned, file-backed, JSON-oriented, and configured with runtime and executor policies.
- `src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowTemplatePackLoader.cs`
  - Confirms manifest/workflow YAML loading, duplicate workflow key detection, runtime policy mapping, executor policy mapping, node/edge/routing conversion, and settings JSON serialization.
- `src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowExampleCatalogSeedService.cs`
  - Confirms managed seeding, seed marker/version behavior, default durable runtime policy, artifact policy, and human-in-loop settings.
- `src/CanDoItAll.Modules.Plugins/CanDoItAll.Modules.Plugins.csproj`
  - Confirms the plugin module references core agent framework abstractions and plugin abstractions.
- `src/plugins/CanDoItAll.Plugin.Email/CanDoItAll.Plugin.Email.csproj`
- `src/plugins/CanDoItAll.Plugin.Gmail/CanDoItAll.Plugin.Gmail.csproj`
- `src/plugins/CanDoItAll.Plugin.Office365/CanDoItAll.Plugin.Office365.csproj`
- `.codex/bundles/workflow-template-examples/README.md`
- `.codex/bundles/workflow-template-examples/subbundles/01-template-pack-file-loading-foundation/README.md`

## External references inspected

- Microsoft Agent Framework documentation root
- Microsoft Agent Framework Workflows overview
- Microsoft Agent Framework Workflows Executors
- Microsoft Agent Framework Workflows Edges
- Microsoft Agent Framework Workflow Builder & Execution
- Microsoft Agent Framework Tools Overview
- Microsoft Agent Framework Tool Approval
- Microsoft Agent Framework Agent Skills
- NuGet package page for `Microsoft.Agents.AI.Workflows`

## Preparation limitation

This bundle was prepared from repository connector reads and public documentation. It does not claim that a local build/test was run during bundle preparation. SB01 intentionally requires a repo-local audit, package restore, build, and targeted tests before Codex changes architecture or runtime behavior.
