# Architecture Checkpoints

## Checkpoint 1: No Partial Growth

Before each subbundle closes:

- Run source assertion for `AgentFrameworkProcessExecutionAdapter.*.cs`.
- Confirm no new partial file was added.
- Confirm moved behavior was deleted from the adapter partial cluster.
- Record adapter cluster line count.

Blocked if:

- A new adapter partial file was added.
- The old adapter still owns the moved responsibility.
- Tests for moved behavior instantiate the adapter.

## Checkpoint 2: Boundary Direction

Before each subbundle closes:

- Inspect changed `.csproj` references.
- Run CodeAnalytics dependency/cycle check when project references or contracts changed.
- Confirm contracts do not reference implementations.
- Confirm runtime does not reference module or domain implementation projects.

Blocked if:

- A cycle appears.
- A contract project references module/UI/infrastructure implementation.
- Runtime directly references a concrete .NET/software-delivery implementation.

## Checkpoint 3: Domain-Free Generic Runtime

Before SB06 and final closure:

- Search generic runtime/dispatcher/adapter orchestration files for forbidden domain terms.
- Classify every hit as allowed protocol/catalog/template/domain-driver implementation or as a leak.
- Remove every leak before closure.

Forbidden in generic logic:

- `Tetris`
- `Calculator`
- `Blazor` product/scaffold decisions
- `qa-validation`
- `quality-accepted`
- `repair-required`
- `repair-escalation`
- `create-dotnet-project`
- `add-test-project`
- `repair-solution-setup`
- `.NET`/`DotNet` process-domain decisions
- `workspace_dotnet_*` lifecycle decisions outside protocol/catalog/domain classifier ownership

## Checkpoint 4: Testability

Before each extracted service closes:

- Direct unit tests exist for the extracted type.
- At least one negative test exists.
- Tests use fakes for external dependencies.
- Tests would fail if production path bypasses the extracted type.

Blocked if:

- Unit tests only assert non-null results or DI resolution.
- Tests need full runtime/adapter construction for pure behavior.
- No failure case exists.

## Checkpoint 5: Pattern Integrity

Before final closure:

- Pattern selection records still match the implemented design.
- No factory owns unrelated business logic.
- No facade became a new monolith.
- No builder invokes external providers.
- No service locator is introduced.

## Checkpoint 6: Pro Root-Cause Closure

Before final closure:

- Branch-aware receipt applicability is implemented and tested.
- Completion gates aggregate issues.
- Branch-routable completion issue route is implemented and tested.
- Safe/idempotent diagnostics route to bounded retry or configured branch before manager escalation.
- Subprocess parent receives child root cause.
- Tool-critical placeholders are resolved or rejected.
- Template/artifact audit covers all relevant templates, not just the example process.

