# Hard Constraints

## Allowed in production source
- New `CanDoItAll.Processes.Core` pure read-models and deterministic rules.
- Module-local adapters that translate process runtime objects into Core descriptors.
- Tests and source scans that guard Core dependencies and driver absence.
- Documentation/test-only driver contract maps.

## Forbidden in this bundle
- Broad Process Core runtime extraction.
- EF, workspace, storage, filesystem, AgentFramework execution, finalizer application, claim lifecycle or transition execution inside Core.
- Production `IProcessDriver*` interfaces, registries, DI registration, runtime dispatchers, manager tools, shell execution drivers, Office/Graph runtime connectors or execution-capable helpers.
- UI, Razor, CSS, JavaScript, TypeScript, image or media changes.
- Small/medium/mobile screenshot proof for this runtime-only bundle.
- Stub/TODO/NotImplemented placeholders in changed production source.
