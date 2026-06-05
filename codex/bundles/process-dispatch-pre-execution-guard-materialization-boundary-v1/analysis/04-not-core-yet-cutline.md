# Not Core Yet Cutline

This bundle must not create or move anything into `CanDoItAll.Processes.Core`.

Reason:

- The current seam still depends on EF writes, transition claims, process journal entries, `ProcessesService.RerunAgentStepAsync`, dispatcher logging, and runtime recovery semantics.
- These are application/runtime orchestration concerns, not pure core rules.
- Driver readiness can be documented now, but production driver APIs need a stable execution/evidence intent vocabulary first.

Allowed:

- module-local helpers under `CanDoItAll.Modules.Processes/Automation/Dispatch`
- request/result records scoped to this module
- explicit side-effect coordinators
- architecture tests that prevent premature core/driver drift
- documentation-only driver readiness map

Forbidden:

- `CanDoItAll.Processes.Core`
- `IProcessDriverPack`
- driver registry
- process driver packages
- moving EF writes into pure helpers
- hiding `SaveAgentAsync`, `TransitionStepWithClaimAsync`, journal writes, or `RerunAgentStepAsync` inside pure-looking planners
