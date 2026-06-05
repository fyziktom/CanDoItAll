# Not-Core-Yet Cutline

This bundle must not create Process Core.

Allowed:

- Module-local helper classes.
- Module-local records and enums used by dispatch only.
- Pure rule helpers for execution-run selection and route planning.
- Documentation-only driver-readiness map.

Disallowed:

- `src/CanDoItAll.Processes.Core`
- `src/CanDoItAll.Modules.Processes.Core`
- `ProcessDriver`, `DriverPack`, or `IProcessDriverPack` production APIs
- Moving EF entities or DbContext logic
- Changing public process tool names
- Changing workflow/agent/subprocess execution semantics
