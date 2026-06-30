# SB09 Codeanalytics Dependency Summary

## Snapshot

- Snapshot id: `snap-20260628165911-672062eb`
- Solution: `CanDoItAll.slnx`
- Scope: MAF, Core capability contracts, Capabilities abstractions/access, Tools abstractions/implementation, Skills abstractions/implementation, MCP abstractions/implementation.
- Project count: `10`
- Document count: `212`

## Project Direction

- `CanDoItAll.AgentFramework.Capabilities.Abstractions` has no project references and is referenced by Access, Core, MAF, MCP, MCP.Abstractions, Skills, Skills.Abstractions, Tools, and Tools.Abstractions.
- `CanDoItAll.AgentFramework.Capabilities.Access` references only `Capabilities.Abstractions`; it is referenced by MAF.
- `CanDoItAll.AgentFramework.Tools` references `Capabilities.Abstractions` and `Tools.Abstractions`; it is referenced by MAF.
- `CanDoItAll.AgentFramework.Skills` references `Capabilities.Abstractions` and `Skills.Abstractions`; it is referenced by MAF.
- `CanDoItAll.AgentFramework.Mcp` references `Capabilities.Abstractions`, Core, and `Mcp.Abstractions`; it is referenced by MAF.
- `CanDoItAll.AgentFramework.Maf` references the isolated abstraction and implementation projects and is not referenced by those isolated projects.

## Cycle Review

- `code_analytics_dependencies_get` on `snap-20260628165911-672062eb` with search text `MafAgentRuntime dependency cycle` returned `cycles: []`.
- The dependency-only snapshot reports `findingCount: 0`, `diagnosticCount: 0`, and no blocking errors.

## Decision

- No new scoped project dependency cycle was introduced by SB09.
- The final project direction preserves isolation: MAF depends on isolated services; isolated service projects do not depend on MAF.
