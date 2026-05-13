# Sample Bundled Plugin

## Status

- `Ready`

## Objective

- Add a small bundled external-service plugin proving settings, secrets, executor, and workflow usage.

## Success Criteria

- A small bundled plugin proves catalog, settings, connection, secret binding, health check, executor catalog, and workflow invocation.
- The sample uses an external-service style operation without requiring OAuth2.
- Failures and outputs are sanitized and size-bounded.
- Browser proof shows the sample plugin end-to-end.

## Covered Inputs

- `R008`
- `R011`
- `R020`
- `R021`
- `R031`
- `F001`
- `F006`
- `F008`

## Prerequisites

- `SB12,SB07`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowExecutorContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowExecutorModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\WorkflowsApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\SecurityModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\SecretRuntimeResolver.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Files\WorkspaceFileContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Files\WorkspaceFileService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Connectors\ConnectorManifest.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj`

## Deliverables

- Bundled sample plugin project/class.
- Manifest with settings schema and one or two workflow executors.
- Connection settings and health check.
- Workflow executor implementation using plugin capability context.
- Unit/integration/component/browser tests.

## Dependency Impact

- Shop, OAuth2, final proof, and future SaaS plugin bundles depend on this MVP being coherent and bounded.

## Validation Depth

- `Plugin MVP implementation`

## Implementation Steps

1. Choose a safe sample such as External Webhook or Mock Mailbox.
2. Define plugin manifest, settings schema, connection schema, required secret reference, and executor descriptor.
3. Register the plugin as bundled/static.
4. Implement health check using configured settings without leaking secrets.
5. Implement executor with timeout/cancellation support, sanitized errors, and payload limit.
6. Add a workflow test that runs the plugin executor.
7. Add UI/browser proof for installing/enabling/configuring/running the sample plugin.

## Scope Exceptions

- No Gmail/Office/Figma real OAuth integration.
- No external network dependency in automated tests; use fake handler/mock endpoint.

## Do Not Do

- Do not require public internet for tests.
- Do not store API token raw in settings.
- Do not let sample plugin use IServiceProvider directly.

## Acceptance Checklist

- [ ] A small bundled plugin proves catalog, settings, connection, secret binding, health check, executor catalog, and workflow invocation.
- [ ] The sample uses an external-service style operation without requiring OAuth2.
- [ ] Failures and outputs are sanitized and size-bounded.
- [ ] Browser proof shows the sample plugin end-to-end.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "SamplePlugin|ExternalWebhook|MockMailbox"`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "SamplePlugin"`
- Browser proof: configure sample plugin, health check, add workflow executor, run/test workflow.

## Browser Validation Logging

- Required. Capture catalog, settings, workflow editor, and run/test evidence.

## Progression Gate

- Passed only when the sample plugin proves the MVP surfaces end-to-end without OAuth2 or dynamic loading.

## Suggested Agent Prompt

```text
Implement SB13 only.

Work outcome-first:
- Read this subbundle README, the root README, and reviews/01-execution-report.md.
- Verify prerequisites and exact source references before editing.
- Preserve the listed scope boundaries.
- Make the smallest correct change set.
- Capture required proof.
- Update reviews/01-execution-report.md.
- Stop if the progression gate cannot honestly pass.
```
