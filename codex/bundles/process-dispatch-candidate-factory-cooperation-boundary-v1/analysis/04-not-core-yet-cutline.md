# Not-Core-Yet Cutline

This bundle must stay inside `CanDoItAll.Modules.Processes`.

Allowed:
- Module-local helper classes.
- Internal records/enums/snapshots used only by the Processes module.
- Tests proving candidate construction parity.
- Documentation-only driver-readiness maps.

Disallowed:
- `CanDoItAll.Processes.Core`.
- Production process driver APIs, registries, or driver packs.
- Moving EF entities or DbContext usage to a new core.
- Public contracts for private dispatcher/finalizer types.
- New dependency from MAF/Tooling/Core to Processes.
- UI or responsive proof work.
