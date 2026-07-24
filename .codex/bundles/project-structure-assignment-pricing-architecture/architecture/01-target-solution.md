# Target Solution

The implemented Project Structure page and Gantt coordinator delegate task-assignment interpretation to one top-level policy. The policy exposes a unique-primary single-choice projection when available, explicit ambiguity/multiplicity state, and whether direct assignment mutation is safe. Mixed direct-assignment sets remain read-only in the scalar dialog, while non-assignment saves preserve every canonical record.

`ProjectStructureTaskResourceCostService` becomes a thin registry/dispatcher over one strategy per strongly typed resource kind. Person pricing reads CRM workforce rate. Agent pricing delegates to existing AgentFramework usage analytics. Workflow and process pricing retain their current bounded historical mechanisms in separate strategies.

A lifecycle-aware estimate refresher applies those quotes to tasks whose explicit execution state is `NotStarted` and clears stale cost when the authoritative source is unavailable. New tasks write `NotStarted`; started/completed/cancelled and legacy `Unknown` tasks keep their historical estimate. UI preview and service mutation paths use the same policy; the existing dialog composition stays intact.

`ProjectStructureTaskApplicationService` is the shared application seam for Gantt and canvas create/edit. Its direct-assignment revision CAS prevents callback races; its compensation helper restores the exact assignment/pricing snapshot when downstream persistence fails. CRM assignment rows and WorkItem metadata are staged in one serializable transaction through the Projects-owned mutation bridge, so the CRM module does not take a Workbench dependency.
