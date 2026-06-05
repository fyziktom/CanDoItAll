# Not Core Yet Cutline

This bundle must not create Process Core.

Allowed:

- module-local helper files in `src/CanDoItAll.Modules.Processes/Automation/Dispatch/`;
- module-local partial files for subprocess dispatch/projection;
- architecture/source tests that prevent premature Process Core and driver APIs;
- documentation-only driver-readiness maps;
- focused parity tests.

Disallowed:

- `CanDoItAll.Processes.Core`;
- `CanDoItAll.Processes.DriverPacks.*`;
- `IProcessDriverPack`, `IProcessDriverRegistry`, production driver descriptors or registries;
- moving EF entities, DbContext usage, storage implementations, transition execution, finalizer execution, or subprocess service calls to a core-style project;
- UI work;
- mobile/small/medium viewport proof.
