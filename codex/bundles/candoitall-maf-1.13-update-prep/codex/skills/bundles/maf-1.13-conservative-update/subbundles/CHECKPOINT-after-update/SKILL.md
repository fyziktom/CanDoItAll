# Checkpoint: Architecture Drift Review

## Goal

Stop accidental refactors before broader validation.

## Review commands

```powershell
git diff --stat
git diff -- src/MAF/Common/CanDoItAll.AgentFramework.Maf src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter
rg "registers .*ProcessAgentRuntimeToolProvider|new ProcessAgentRuntimeToolProvider|class ProcessAgentRuntimeToolProvider|Add.*ProcessAgentRuntimeToolProvider|Current direct runtime tools: 23|/api/processes/definitions|/api/processes/templates|/api/processes/runs/\\{runId\\}/detail|ProcessManagerTools" docs src tests -g "*.md" -g "*.cs" -g "*.json"
rg "Microsoft\.Agents\.AI\" Version=\"1\.8\.0|Microsoft\.Agents\.AI\.OpenAI\" Version=\"1\.8\.0|Microsoft\.Agents\.AI\.Workflows\" Version=\"1\.8\.0" src tests tools -g "*.csproj"
git diff --check
```

## Reject conditions

Reject and revise if the diff:

- adds `ProcessAgentRuntimeToolProvider`,
- expands `/api/processes`,
- moves process-domain behavior into MAF infrastructure,
- introduces central package management,
- removes governance/finalizer/approval evidence,
- broadens warning suppression,
- updates unrelated package families,
- performs large refactors unrelated to package API breaks.

## Exit criteria

- Diff is small and architecture-preserving.
- Package references are updated.
- No stale MAF 1.8 stable references remain.
