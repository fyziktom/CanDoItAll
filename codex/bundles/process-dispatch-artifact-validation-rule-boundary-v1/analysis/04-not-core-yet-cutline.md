# Not Process Core Yet Cutline

This bundle may create or extend helper classes under `CanDoItAll.Modules.Processes/Automation/Dispatch` or another Processes-owned non-Core folder.

It must not:

- create `CanDoItAll.Processes.Core`,
- move EF entities,
- move dispatcher state types,
- move storage placement or DB recording to Core,
- introduce driver pack abstractions,
- require MAF/Tooling to reference Processes again.

## Final Red-Team Result

The artifact validation work remains below the Process Core line:

- validation snapshots and rule helpers stay local to `CanDoItAll.Modules.Processes`;
- dispatcher orchestration still owns EF, storage placement, artifact writes, expectation recording, and runtime state mutation;
- helper classes do not reference MAF, Tooling, product modules, file APIs, directory APIs, DbContext, or driver-pack names;
- no `CanDoItAll.Processes.Core`, `ProcessDriver`, `DriverPack`, or `IProcessDriverPack` production surface was introduced;
- browser proof remains N/A because no UI changed, and no prohibited small/medium/mobile proof artifacts were created.

## Next Dispatcher Cutline

The next safe cutline is still module-local, not Process Core:

1. Isolate tool validation and recovery/finalization decision rules as pure Processes-owned helpers.
2. Keep runtime orchestration, persistence, MAF invocation, storage, and artifact recording in the existing dispatcher/application-service boundary.
3. Add architecture guards before any future move that would let Process Core or driver-pack terminology enter production code.

Process Core should be revisited only after validation rules, tool validation, and recovery/finalization seams are independently isolated, covered, and proven to have no persistence or product-module dependencies.
