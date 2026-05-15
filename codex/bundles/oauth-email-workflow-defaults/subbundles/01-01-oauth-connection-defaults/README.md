# 01 OAuth Connection Defaults

## Status

- `Completed`

## Objective

Make Gmail and Office365 workflow executors resolve a blank `connectionId` from the connected OAuth account configured in Plugin settings.

## Covered Inputs

- `N001`
- Requirements `R001`, `R002`

## Prerequisites

- Bundle prepared-stage gate passes.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\OAuth\PluginOAuthService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Gmail\GmailWorkflowExecutor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Office365\Office365WorkflowExecutor.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs`

## Deliverables

- Shared OAuth connection-id resolver for blank executor settings.
- Gmail and Office365 executors use the resolver before requesting access tokens.
- Documentation no longer requires manual connection id lookup for these email workflows.

## Dependency Impact

- `02` depends on this because the Project Structure start flow cannot validate Office365 workflow preview/start behavior if email download fails before storage.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Add a `PluginOAuthService` resolver that returns an explicit configured id or selects the newest enabled connected OAuth record for the plugin connection key.
2. Fail invalid explicit ids and missing/invalid connected OAuth records with actionable errors.
3. Update Gmail download, Gmail mark-processed, and Office365 download executors.
4. Add targeted integration/unit coverage.

## Scope Exceptions

- Do not add UI account selection in this phase; blank means auto-select the newest valid connected account.

## Do Not Do

- Do not silently use reconnect-required OAuth records.
- Do not introduce a parallel token store or read token material in workflow settings.

## Acceptance Checklist

- Blank Office365 `connectionId` resolves to the saved Office365 OAuth connection.
- Blank Gmail executor settings resolve to the saved Gmail OAuth connection.
- Non-empty invalid ids still fail.
- Missing connected OAuth state reports a clear error.

## Proof Required

- Targeted plugin integration tests.
- Targeted workflow executor tests if practical.
- `dotnet build src\CanDoItAll.Web\CanDoItAll.Web.csproj`.

## Browser Validation Logging

- N/A for this backend-only subbundle.

## Progression Gate

- Downstream subbundle may start only after targeted tests prove blank `connectionId` no longer blocks email plugin executors.

## Suggested Agent Prompt

```text
Implement this subbundle only. Add shared OAuth connection-id resolution in the plugin OAuth service, wire Gmail and Office365 workflow executors to it, keep explicit errors for invalid configuration, and prove the behavior with targeted tests before moving to the Project Structure start-dialog work.
```
