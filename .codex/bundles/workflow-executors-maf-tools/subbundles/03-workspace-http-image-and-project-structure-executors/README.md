# Workspace HTTP image and project structure executors

## Status

- `Ready`

## Objective

- Add the first non-document generic executors: workspace file/storage, HTTP/HTTPS, project-structure access/assets, and AI image generation.

## Success Criteria

- Storage executor uses existing bounded workspace file service operations.
- HTTP executor validates scheme, method, headers, timeout, and response size.
- Project-structure executor can read tree/node data and create typed asset nodes when required services are available.
- Image executor calls existing provider path or fails with explicit provider-service unavailability.

## Covered Inputs

- R06, R07, R08, R09, R12, R17.

## Prerequisites

- Subbundle 01 contracts compile.
- Existing workspace file and project/image service registrations are identified.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Files\WorkspaceFileContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workspace\WorkspaceFileToolModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Tools\MafAgentRuntime.ProjectStructureTools.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Tools\MafAgentRuntime.ImageGenerationTools.cs`

## Deliverables

- Storage executor settings/result contracts and implementation.
- HTTP executor settings/result contracts and implementation.
- Project-structure executor adapter or explicit unavailable-service implementation with descriptor preserved.
- Image executor adapter or explicit unavailable-service implementation with descriptor preserved.
- Tests for storage and HTTP success/failure cases.

## Dependency Impact

- Subbundle 04 depends on these implementations for runtime invocation and event/artifact mapping.
- Subbundle 06 depends on them for the 20-scenario matrix.

## Validation Depth

- `Critical runtime tool foundation`

## Implementation Steps

1. Implement storage executor operations for list, stat, read, write, append, search, and diff.
2. Implement HTTP executor with typed method enum, URI scheme validation, bounded response bytes, and sanitized headers in failure logs.
3. Inspect project-structure service boundaries and implement the smallest adapter that supports read tree/node and asset creation, or return explicit unavailable-service failure.
4. Inspect image provider/tool boundary and implement prompt-to-artifact execution, or return explicit provider blocker.
5. Register executors through the shared catalog/DI path.
6. Add focused tests for storage and HTTP; add service-unavailable tests for project/image when host services are absent.

## Scope Exceptions

- Full project-structure editing beyond read/subtree/asset creation is out of scope.
- Browser downloads, cookies, redirects beyond default `HttpClient` behavior, and HTML rendering are out of scope for HTTP fetch.
- Image model selection UI is out of scope; the executor should accept typed provider/model settings and let provider registration decide availability.

## Do Not Do

- Do not bypass existing workspace path guards.
- Do not allow `file:`, `ftp:`, or arbitrary schemes in HTTP executor.
- Do not fake project or image success if required services are unavailable.

## Acceptance Checklist

- Storage read/write scenarios operate on bounded workspace paths.
- HTTP invalid URI/scheme fails validation or execution predictably.
- Project/image unavailable states include executor id and missing service/provider detail.
- Catalog exposes all descriptors.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter WorkflowExecutor`
- At least one real HTTP scenario result captured in execution report.
- Update subbundle gate row.

## Browser Validation Logging

- `N/A`

## Progression Gate

- Subbundle 04 may continue only after storage and HTTP tests pass and project/image availability behavior is explicit.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
