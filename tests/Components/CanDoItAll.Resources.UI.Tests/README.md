# CanDoItAll.Resources.UI.Tests

## Purpose

Portable deterministic tests of the actual Resources controllers, renderer, configuration fields, real shared file descendants and dependency closure.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework: `net10.0`

```powershell
dotnet test tests/Components/CanDoItAll.Resources.UI.Tests/CanDoItAll.Resources.UI.Tests.csproj --configuration ResourcesUiProof /m:1
```

## Dependencies

The evaluated dependency inputs are in [CanDoItAll.Resources.UI.Tests.csproj](CanDoItAll.Resources.UI.Tests.csproj).
The [Resources boundary receipt](../../../docs/architecture/resources-ui-boundary.md)
records the evaluated source-mode closure, validation and compatibility decisions.

## Architecture Notes

Tests exercise immutable submissions, stable raw drafts/EditContext, unknown/known outcomes, exact review, route lifetimes, stale acquisitions and slow cleanup, held/failing parent callbacks, real search/paging, file effects and wire compatibility. They need no PostgreSQL, live transport, filesystem storage or local application launch. Production authority and durable acknowledgement faults are tested separately in Integration and the real Web browser host.

## Validation

Derive expected discovery from current source; build-backed `--list-tests` comes before
the same-filter `--no-build --no-restore` execution. The namespace selection is
`FullyQualifiedName~CanDoItAll.Tests.Components.ResourcesUi`. The project is registered
in the Components and Stable solution filters and every CI component-shard project list.
Runtime closure traversal rejects forbidden and unresolved transitive edges. Do not
replace real owner/PG and browser proof with this scenario-host test layer.
