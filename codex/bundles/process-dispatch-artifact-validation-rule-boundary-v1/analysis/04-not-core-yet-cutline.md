# Not Process Core Yet Cutline

This bundle may create or extend helper classes under `CanDoItAll.Modules.Processes/Automation/Dispatch` or another Processes-owned non-Core folder.

It must not:

- create `CanDoItAll.Processes.Core`,
- move EF entities,
- move dispatcher state types,
- move storage placement or DB recording to Core,
- introduce driver pack abstractions,
- require MAF/Tooling to reference Processes again.

The next Process Core candidate should be revisited only after validation rules, tool validation, and recovery/finalization seams are independently isolated and covered.
