# Current State Analysis

## Solution and module shape

The solution includes a dedicated agent framework area, a MAF integration project, an AgentFramework UI/module project, a Plugins module, and separate plugin projects. This is a good separation point: MAF integration should remain isolated enough that repository workflow/domain models do not leak low-level MAF APIs everywhere.

Relevant project groups observed:

- `src/CanDoItAll.AgentFramework.Core`
- `src/CanDoItAll.AgentFramework.Models`
- `src/CanDoItAll.AgentFramework.Maf`
- `src/CanDoItAll.AgentFramework.Persistence`
- `src/CanDoItAll.Modules.AgentFramework`
- `src/CanDoItAll.Modules.Plugins`
- `src/plugins/CanDoItAll.Plugin.Email`
- `src/plugins/CanDoItAll.Plugin.Gmail`
- `src/plugins/CanDoItAll.Plugin.Office365`
- `src/plugins/CanDoItAll.Plugin.Docker`

## MAF package baseline

The dedicated MAF integration project references:

- `Microsoft.Agents.AI` `1.6.2`
- `Microsoft.Agents.AI.OpenAI` `1.6.2`
- `Microsoft.Agents.AI.Workflows` `1.6.2`
- `Microsoft.Agents.AI.A2A` `1.6.2-preview.260521.1`
- `Microsoft.Agents.AI.Mem0` `1.0.0-preview.251028.1`

NuGet shows `Microsoft.Agents.AI.Workflows` `1.7.0` as available and newer than `1.6.2`. SB01 must decide whether to upgrade to `1.7.0` now or remain on `1.6.2` temporarily with an explicit rationale and test plan.

## Workflow template pack

`Templates/Workflows/manifest.yaml` already has useful production-minded defaults:

- File-backed template pack identity and seed version.
- Shared LLM component model settings.
- Strict JSON-output instructions.
- JSON shape metadata.
- Runtime policy preferring `DurableTask`, allowing in-process previews, and requiring durable production runs.
- Executor policies (`fast`, `slow`) with timeout/retry/artifact capture settings.
- Node instruction defaults.
- External workflow files under `Templates/Workflows/workflows/`.

## Template loader

`WorkflowTemplatePackLoader` already provides a valuable foundation:

- Locates `Templates/Workflows/manifest.yaml` from known execution roots.
- Loads manifest and listed workflow files from YAML.
- Detects duplicate workflow keys.
- Maps template graph nodes and edges to repository workflow models.
- Maps executor ID, executor settings JSON, and execution policy.
- Maps route metadata to `WorkflowEdgeRouting` using `BuiltInJsonV1`.
- Validates enum values and executor policy limits.

## Seed service

`WorkflowExampleCatalogSeedService` provides managed example seeding and already avoids overwriting non-managed workflow definitions by checking the seed marker. It also seeds default workflow settings with durable runtime preference, artifact capture settings, and human-in-loop approval settings.

## Current architectural concern

The repository has a strong workflow domain model and template layer, but the hardening question is whether runtime execution fully uses newer MAF workflow semantics. The suspicious gap is not the template pack itself; it is the boundary between repository-defined workflow graphs and native MAF `WorkflowBuilder`/executor execution. Codex must identify whether this boundary exists, whether it is typed, whether plugin executors participate safely, and whether runtime events/checkpoints align with MAF superstep execution.
