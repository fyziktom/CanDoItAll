# Not Process Core Yet Cutline

Allowed in this bundle:

- Module-local helper classes under `CanDoItAll.Modules.Processes/Automation/Dispatch`.
- Snapshot records that remove direct dependency on dispatcher nested types for tool-validation rules.
- Architecture tests that prove helpers are pure enough for future extraction.
- Documentation-only driver-readiness inventory.
- Focused migration of required-tool and critical-failure consumers through helper wrappers.

Not allowed:

- `CanDoItAll.Processes.Core`.
- Any `ProcessDriver`, `DriverPack`, `IProcessDriverPack`, or domain-specific helper driver production API.
- Moving EF, storage, file I/O, provider fallback mutation, final step transition, recovery journal persistence, or template/process definition mutation.
- Adding dependencies from MAF/Tooling back to product modules.
