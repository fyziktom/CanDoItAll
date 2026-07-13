# Hard Constraints

## Architecture Constraints

- Do not add new partial classes for the adapter or runtime integration responsibilities.
- Do not create nested classes as architecture boundaries.
- Do not create broad helpers or managers such as `ProcessRuntimeHelper`, `AdapterCommon`, or `DotNetThingsManager`.
- Do not solve cycles by dumping types into a broad shared project.
- Do not add `IServiceProvider` service-location inside domain policies or extracted services.
- Do not call `BuildServiceProvider` during registration.
- Do not duplicate moved behavior in both the adapter and the extracted service.

## Domain Boundary Constraints

Generic process runtime, dispatcher, and generic adapter orchestration must not hardcode:

- `.NET`, `dotnet`, `workspace_dotnet_*`, or `workspace_pwsh_run_script` behavior as process-domain decisions.
- Blazor scaffold concepts.
- Tetris or Calculator concepts.
- Software-delivery step keys such as `create-dotnet-project`, `add-test-project`, `repair-solution-setup`, `qa-validation`, or `qa-recheck`.
- Branch outcome keys such as `quality-accepted`, `repair-required`, or `repair-escalation` in generic logic.

Allowed exceptions:

- Tool protocol/catalog projects may define tool names as external protocol constants.
- Process templates and driver implementations may contain domain-specific keys and tool names.
- Generic runtime may carry branch outcome keys and tool names as data.

## Testing Constraints

- Unit tests for extracted services must not require a full app host, database, live workspace, network, external API, or real file system unless the behavior under test explicitly abstracts that dependency.
- Tests must include negative cases for unsupported driver/policy, missing receipt, unresolved placeholder, unsafe retry, child blocked state, and forbidden generic-domain leakage.
- Tests must prove that adding a new domain driver or receipt lifecycle classifier does not require editing the adapter partial cluster.

## Closure Constraints

Final closure is blocked until:

- The adapter partial cluster is deleted or reduced to a documented temporary shell with no extracted responsibilities left inside.
- `WorkspaceCommandReceiptWriter` no longer contains `IsDotNetRuntimeLifecycleTool` or equivalent hardcoded .NET lifecycle classification.
- Generic runtime/dispatcher/adapter files pass forbidden-domain source assertions.
- CodeAnalytics dependency/cycle check passes after implementation.
- Targeted unit tests and relevant process regression tests pass.

