# Target Solution

## Summary

Introduce three prerequisite boundaries:

- a MAF context contribution boundary,
- read-only source snapshot contracts for Workbench/project structure,
- read-only source/event contracts for Process and Workflow runtime evidence.

These boundaries let Cognitive Memory consume agent context and source evidence without owning MAF internals or existing module persistence.

## MAF Boundary

```text
Module registrations
  -> IAgentRuntimeContextContributor registrations
  -> MAF context composition
  -> ordered context contributions
  -> provider trace and policy result
```

The boundary should expose:

- contributor id,
- order/priority,
- supported capability or context scope,
- policy/access context,
- cancellation,
- structured success/skip/failure result,
- trace metadata.

## Source Snapshot Boundary

```text
Workbench/Process/Workflow owners
  -> source snapshot provider
  -> source manifest and source items
  -> future Cognitive Memory source ingestion
```

The boundary should expose:

- source system and source kind,
- project/process/workflow scope,
- stable source key,
- content hash,
- source updated timestamp,
- scan cursor,
- layout and relation metadata,
- storage reference for large immutable payloads,
- provenance and permission context.

## Dependency Direction

Existing modules implement provider contracts. Cognitive Memory later consumes those contracts.

```text
CanDoItAll.AgentFramework.Core or low-level abstractions
    <- MAF adapter implements context composition
    <- Workbench implements project source snapshot provider
    <- Processes/Workflow implement runtime evidence source providers
    <- Cognitive Memory consumes providers later
```

## Projected Cognitive Memory Impact

- `00-prerequisite-boundary-gate` blocks implementation until this bundle is accepted.
- `02-workbench-and-source-ingestion` consumes source snapshots and does not read Workbench tables ad hoc.
- `06-consolidation-engine` consumes Process/Workflow source events and does not reinterpret runtime persistence directly.
- `07-maf-workflow-integration` registers a context contributor and does not edit private MAF composition for a special case.
