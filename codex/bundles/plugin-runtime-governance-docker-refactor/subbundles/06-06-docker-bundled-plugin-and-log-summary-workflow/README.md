# SB06 Docker Bundled Plugin And Log Summary Workflow

## Status

- `Completed`

## Objective

- Add a simple bundled Docker plugin and sample workflow that prove the generic plugin runtime can safely list containers, pull/start a container, read bounded logs, hand logs to an LLM summary step, and start or verify a Qdrant vector database container through the workflow path.

## Success Criteria

- Docker plugin uses generic host-tool recipes from SB03.
- Docker plugin requires explicit grants from SB02/SB04 and workflow enforcement from SB05.
- Log summary workflow uses a separate LLM node, not privileged Docker plugin access to LLM credentials.
- End-to-end proof starts or verifies Qdrant through the plugin workflow path while Docker is running.

## Covered Inputs

- `N006`: Docker plugin can inspect running containers, pull/start Docker, and get logs.
- `N007`: workflow includes LLM summary of logs.
- `N008`: plugins remain generic.
- `N011`: Qdrant vector database container must be started through workflow proof.
- Requirements `R007`, `R009`, `R010`, `R014`, and `R015`.

## Prerequisites

- SB03 host-tool recipes are complete.
- SB04 user grants can enable Docker recipe access.
- SB05 workflow plugin bridge enforces grants at validation and runtime.
- Deterministic Docker recipe test doubles exist for CI; real Docker CLI smoke is optional.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Plugins.Abstractions\PluginManifestContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Plugins.Abstractions\PluginExecutionContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Commands\WorkspaceCommandExecutionService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Commands\WorkspaceCommandPlanBuilder.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowExecutorContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowExecutorObservability.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowExecutorModels.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs

## Deliverables

- Bundled Docker plugin descriptor with workflow executors for list containers, pull image, start container, and read logs.
- Executor settings schemas with typed, validated fields for image reference, registry policy, container name, tail count, since timestamp, timeout, and max characters.
- Sample workflow template or fixture: Docker read logs -> LLM summary -> artifact/result.
- Sample Qdrant workflow template or validation fixture: Docker pull/start Qdrant -> Docker logs -> summary-compatible workflow step.
- Tests using fake recipe runner output for list, pull, start, logs, missing Docker CLI, invalid image reference, missing grant, and oversized logs.
- Optional local Docker CLI smoke test instructions that are skipped unless explicitly enabled.

## Dependency Impact

- SB07 uses the Docker sample to verify output payload, artifact storage, audit, and EF behavior.
- SB08 uses the sample workflow as the end-to-end architecture proof.

## Validation Depth

- `End-to-end sample proof`

## Implementation Steps

1. Add bundled Docker plugin project/module registration using existing plugin registration patterns.
2. Add plugin workflow executor descriptors and settings schemas.
3. Implement executors that call generic host-tool recipes only.
4. Shape results with bounded payloads, truncation flags, receipt references, and artifact references for large logs.
5. Add sample workflow fixture or template that pipes logs into an LLM summary node.
6. Add deterministic tests with fake recipe runner responses.
7. Optionally document a local Docker CLI smoke path without making CI depend on Docker.
8. Validate the Qdrant workflow through plugin APIs and workflow APIs, not direct database mutation.
9. Update execution report with sample workflow proof.

## Scope Exceptions

- No arbitrary Docker command executor.
- No container orchestration UI.
- No direct LLM calls from Docker plugin code.
- No privileged Docker modes or arbitrary mounts.

## Do Not Do

- Do not expose raw PowerShell or raw Docker CLI arguments.
- Do not add Docker-only concepts to plugin-core abstractions.
- Do not store Docker logs directly in EF.
- Do not pass LLM API keys into Docker recipe environments.

## Acceptance Checklist

- Docker plugin is installed/visible as a bundled plugin.
- Without grants, Docker executors are unavailable or denied.
- With grants, fake recipe tests prove list, pull, start, and logs behavior.
- Logs are capped before plugin payload return.
- LLM summary step is a separate workflow node.
- Missing Docker CLI produces actionable unavailable result.

## Proof Required

- Unit/integration test command and result for Docker plugin behavior with fake recipes.
- Workflow validation or run proof showing Docker logs feed LLM summary.
- Host proof that the workflow path starts or verifies a Qdrant container.
- Evidence that oversized logs produce truncation/artifact metadata instead of oversized payloads.
- Optional Docker CLI smoke result if a local environment explicitly enables it.

## Browser Validation Logging

- Route: workflow run/details route if the sample workflow result is browser-visible.
- Viewport: large-screen pass when route is affected.
- Playwright actions: open workflow/run details, assert Docker log node result, assert LLM summary node output, assert truncation/artifact metadata when available.
- Screenshots: sample workflow result if browser-visible.
- Review questions: output must not leak secrets, summary must clearly show whether logs were truncated, and route must not present denied Docker access as success.

## Progression Gate

- SB07 may start only after the Docker sample proves generic host-tool recipes, explicit grants, bounded logs, and separate LLM summary workflow behavior.

## Suggested Agent Prompt

```text
Implement SB06 only.
Add the bundled Docker sample plugin and log-summary workflow using the generic host-tool and workflow bridge contracts. Keep Docker-specific code out of plugin-core abstractions and use deterministic fake recipe tests.
```
