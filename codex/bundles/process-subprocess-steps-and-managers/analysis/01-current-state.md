# Current State

## Process Model

- `ProcessStepKind` is flat: start, work, decision, approval, review, delivery, and end.
- Definition editor and persistence models have no subprocess reference on steps.
- Runtime runs and step runs have no parent-child relation.
- Process runs are already persisted separately from AgentFramework executions, which is the right place to model process-tree truth.

## Runtime

- Process run start creates run, role assignments, step runs, work briefs, journal entries, project structure sync entries, and outbox entries.
- Automation dispatch already executes step work through AgentFramework with process run and step ids in the execution source context.
- Existing dispatch and progression services should be reused for concurrency and recovery. Adding observer threads per subprocess would be the wrong shape.

## Manager And HR

- HR matching exists in `ProcessesService.Launch.Staffing.cs`.
- AgentFramework seeds include an HR Staffing Manager and several .NET implementation agents.
- Agent definitions live in AgentFramework storage, so process entities should store manager agent ids as values and snapshots rather than database foreign keys.

## Canvas And UI

- Process canvas already has step nodes, role nodes, context actions, chrome actions, and double-click/open handling.
- Step editor exposes `ProcessStepKind` but has no subprocess selector.
- Current template pack contains software-delivery and other flat templates, but no reusable .NET subprocess template.

## Agent Framework 1.3 Analysis

- `C:\repositories\agent-framework\dotnet\samples\03-workflows\_StartHere\05_SubWorkflows\Program.cs` demonstrates sub-workflows bound as executors.
- MAF 1.3 workflow samples expose workflow events, executor completed/failed events, checkpoints, A2A cards, continuation tokens, and handoffs.
- CanDoItAll should use those capabilities at the AgentFramework adapter boundary, not as persisted process runtime entities.
