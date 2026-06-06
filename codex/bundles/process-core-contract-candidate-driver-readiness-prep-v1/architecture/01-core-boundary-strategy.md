# Core Boundary Strategy

## Not This Bundle
This bundle must not create:
- `src/CanDoItAll.Processes.Core`
- `src/CanDoItAll.Modules.Processes.Core`
- any production process driver project
- `IProcessDriverPack`
- `IProcessDriverRegistry`
- `ProcessDriverRegistry`

## Candidate Core Areas
Only after this bundle passes should these be considered for a future narrow Core extraction:
- route stage order and route kind decisions,
- pure subprocess lifecycle status mapping,
- pure transition request shaping,
- pure artifact expectation matching helpers,
- pure validation/satisfaction rule families,
- pure driver-readiness descriptors.

## Application/Infrastructure Areas That Must Stay Out Of Core
- EF-backed candidate hydration,
- claim lease and heartbeat,
- storage/file/workspace artifact projection,
- AgentFramework execution,
- technical-agent binding,
- project-structure access mutation,
- materialization journal/rerun side effects,
- workflow execution coordinator.
