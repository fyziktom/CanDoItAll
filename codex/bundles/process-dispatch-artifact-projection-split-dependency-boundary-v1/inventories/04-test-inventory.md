# Test Inventory

Required proof commands should be adapted to the repo's actual current test names.

## Minimum proof

- `dotnet build CanDoItAll.slnx --no-restore`
- focused unit architecture tests for:
  - no Process Core,
  - no driver API,
  - no nested coordinator reintroduction,
  - no broad dispatch-service dependency.
- focused unit projection tests for candidate-state mutation and projection order.
- focused integration tests for:
  - execution artifact projection,
  - process mock artifact projection,
  - workspace-written artifact projection,
  - existing managed artifact projection,
  - response-text artifact projection,
  - provider-native browser artifact projection,
  - completed-decision artifact record-only path.

## Broad smoke matrix

Include previously created focused test slices from:
- artifact validation residual boundary,
- observation/outcome boundary,
- execution/retry/provider boundary,
- subprocess runtime/projection boundary,
- pre-execution/materialization boundary.
