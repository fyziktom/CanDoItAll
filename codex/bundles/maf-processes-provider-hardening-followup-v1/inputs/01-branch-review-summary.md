# Branch Review Summary

Reviewed branch: `maf-processes-refactor` against `development`.

Observed successful scoped outcomes:

- `CanDoItAll.slnx` includes new `src/CanDoItAll.AgentFramework.Tooling/CanDoItAll.AgentFramework.Tooling.csproj`.
- `CanDoItAll.AgentFramework.Maf.csproj` no longer directly references `CanDoItAll.Modules.Processes`.
- `CanDoItAll.AgentFramework.Tooling` is small and depends only on `CanDoItAll.AgentFramework.Models` plus `Microsoft.Extensions.AI.Abstractions`.
- MAF capability composition resolves `IEnumerable<IAgentRuntimeToolProvider>` and attaches registered provider tools deterministically.
- Processes registers `ProcessAgentRuntimeToolProvider` through `TryAddEnumerable`.
- `ProcessAgentRuntimeToolProvider` exposes the expected process tool surface and preserves access checks in the Processes module.
- The executed bundle's red-team review states the scoped objective passed and records residual scope explicitly.

Observed remaining work:

- MAF still references product modules such as Projects, Security, Workbench, and Workspace. Some may still be legitimate, but they need a providerization/allowed-list plan.
- MAF still hard-codes project-structure and image-generation tool attachment functions.
- The new `IAgentRuntimeToolProvider` seam returns raw `AITool` lists without provider descriptors, tool ownership metadata, operation kind, or explicit evidence/security classification.
- `ProcessAgentRuntimeToolProvider` is a large file and should be split before more process/tool hardening lands.
- Provider context has `Purpose`, `RuntimeSessionKey`, and `Tags`, but process provider currently creates tools from `context.Agent` and does not yet use purpose-aware policy.
- The branch diff includes large `codex/bundles` churn; clean before merge.
