# ADR: Conservative Microsoft Agent Framework 1.13 Update

Status: Proposed  
Date: 2026-07-07  
Branch: `memory-providers`

## Context

The branch currently references Microsoft Agent Framework 1.8-era packages in the MAF adapter projects. Newer MAF releases provide improvements that may help later with agent work, workflows, checkpointing, skills, approvals, HITL, and observability. However, the first stage must not become a broad feature adoption or architectural rewrite.

## Decision

Update the stable Microsoft Agent Framework packages to `1.13.0` and align direct dependency-floor packages required by MAF 1.13. Fix only breaking changes caused by this package update.

## Accepted first-stage changes

- Update `Microsoft.Agents.AI` to `1.13.0`.
- Update `Microsoft.Agents.AI.OpenAI` to `1.13.0`.
- Update `Microsoft.Agents.AI.Workflows` to `1.13.0`.
- Update `Microsoft.Extensions.AI.Abstractions` to `10.6.0`.
- Update `Microsoft.Extensions.DependencyInjection.Abstractions` to `10.0.9`.
- Verify `Microsoft.Agents.AI.A2A` and `Microsoft.Agents.AI.Mem0` with NuGet CLI before changing them.
- Apply minimal source changes inside existing MAF adapter seams if package API signatures changed.

## Rejected first-stage changes

- Reintroducing direct process runtime tools.
- Expanding process HTTP APIs.
- Moving process-domain contracts into MAF.
- Adopting Foundry hosting or Durable workflows.
- Adopting new FileAccess/FileMemory/file editing tool surfaces as features.
- Broad refactors of `MafAgentRuntime` or `RuntimeCapabilityComposer`.
- Introducing central package management.
- Suppressing warnings broadly instead of adapting to API changes.
- Weakening finalizer, structured-output, approval, provider-gate, or evidence behavior.

## Consequences

The update should unblock later evaluation of new MAF capabilities while keeping current CanDoItAll process and agent behavior stable. The next phase can separately analyze whether MAF 1.13 workflow/checkpointing/HITL/skill-source improvements should be adopted intentionally.
