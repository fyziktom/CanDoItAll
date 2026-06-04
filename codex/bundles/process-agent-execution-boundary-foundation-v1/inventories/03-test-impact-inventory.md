# Test Impact Inventory

| Test area | Expected coverage |
| --- | --- |
| Static architecture | MAF product-tool dependency remains forbidden |
| Tooling neutrality | Tooling does not reference product modules |
| Provider composition | Processes/Workbench/Image providers still attach |
| Process provider parity | 23 process tools remain exact |
| Project-structure provider parity | 28 project-structure tools remain exact |
| Image provider parity | `image_generation_create` remains exact |
| Dispatcher execution boundary | direct execution calls move behind facade |
| Process outbox | process dispatch/outbox behavior still passes |
| Receipt semantics | runtime provider metadata and required-tool receipts survive |
| Artifact lineage | current-run artifact lineage still passes |
| Integration smoke | process-filtered integration tests pass |
| Full build | `dotnet build CanDoItAll.slnx` passes |
