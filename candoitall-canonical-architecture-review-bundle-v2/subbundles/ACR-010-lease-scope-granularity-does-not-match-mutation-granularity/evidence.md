# Evidence

## Code evidence

- src/CanDoItAll.Modules.Workbench/ProjectStructureAgentContracts.cs:26-74 defines Project and ProjectNode lease scopes
- src/CanDoItAll.Modules.Workbench/ProjectStructureLeaseService.cs:197-222 project-scoped acquisition helper still dominates current flow
- Previous bundle and current static scan did not find a broad rollout of node-level scope selection in mutation entry points

## Root cause

The runtime safety model was introduced conservatively before mutation invariants and canonical ownership were stable enough for finer-grained locks.

## Architectural interpretation

This finding exists because the current code lets one concern occupy too many roles at once.
