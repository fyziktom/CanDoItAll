# Required SharedInfo skills

## Analyzed SharedInfo baseline

- Repository: `fyziktom/CanDoItAll.SharedInfo`
- Branch: `main`
- Commit: `67a5e73a6f80ae3d7c8afcee56f9e7cde48b5939`

Claude Code must use the installed skill copies when available. Repository paths below identify the reviewed source versions.

| Skill | Source path | Required use in this bundle |
|---|---|---|
| C# Architecture Governor | `codex/skills/csharp-architecture-governor/SKILL.md` | Before every architecture phase; maintain responsibility and boundary maps |
| C# Modular Refactoring | `codex/skills/csharp-modular-refactoring/SKILL.md` | Split runtime, context factory, capability composition, and process leakage by responsibility |
| C# Project Boundary Extraction | `codex/skills/csharp-project-boundary-extraction/SKILL.md` | Runtime abstractions and Security abstractions; project-reference repair |
| C# Factory/Builder Composition | `codex/skills/csharp-factory-builder-composition/SKILL.md` | Workspace runtime services factory, adapter composition, contributor catalogs |
| Provider/Tool/Plugin Isolation | `codex/skills/csharp-provider-tool-plugin-isolation/SKILL.md` | Keep tools/providers/MCP/runtime contributions out of the runtime facade |
| C# Dependency Graph Audit | `codex/skills/csharp-dependency-graph-audit/SKILL.md` | Before and after every `.csproj` change; cycle and forbidden-reference proof |
| C# Testability Contracts | `codex/skills/csharp-testability-contracts/SKILL.md` | Characterization, direct unit, negative, and composition tests |
| Canonical Model Review | `codex/skills/architecture-reviews/canonical-model-review/SKILL.md` | Classify product truth, UI observation, chat binding, turn context, authority, execution and adapter state |
| C# Architecture Review Gate | `codex/skills/csharp-architecture-review-gate/SKILL.md` | Blocking checkpoint after each foundation wave |
| C# Architecture Bundle Guard | `codex/skills/bundles/candoitall-csharp-architecture-bundle-guard/SKILL.md` | Required bundle files, README sections, proof, and checkpoint discipline |


## Claude Code skill/tool mapping

The reviewed skill sources are stored under the SharedInfo repository's historical `codex/` directory, but their architecture content is executor-independent. Use the installed Claude Code copies that expose the same skill intent. Do not rename or rewrite the source paths in evidence reports.

Also use the installed official C#/.NET skills for language, SDK, build, test, dependency-injection, async/disposal, and package guidance. The SharedInfo architecture skills remain authoritative for project boundaries, source-of-truth ownership, bundle checkpoints, and anti-shallow-refactor rules. When a generic .NET recommendation conflicts with an accepted bundle ADR or dependency direction, stop and resolve the architecture conflict rather than silently following either instruction.

Use the installed bundle execution skill for proof-manifest/checkpoint discipline and CodeAnalytics MCP for repository evidence. Tool availability must be recorded; missing MCP/skill access is a validation gap, not permission to invent findings.

## Supporting references

- `codex/skills/_csharp-architecture-shared/references/bundle-architecture-sections.md`
- `codex/csharp-architecture/integration/architecture-bundle-profile.md`
- `codex/csharp-architecture/examples/maf-runtime-capabilities-extraction.md`
- `codex/csharp-architecture/checklists/large-class-refactoring-checklist.md`

## Mandatory behavior

1. Use CodeAnalytics MCP first when it is available.
2. Record snapshot IDs and dependency/cycle evidence in each proof manifest.
3. Extract one cohesive responsibility at a time.
4. Add direct tests for an extracted owner before closing its subbundle.
5. Add negative tests that reject shallow separation.
6. Run the architecture review gate at SB05, SB08, SB11, SB14, SB17 stabilization, and SB18 final release.
7. Stop when dependency direction, authority ownership, or testability cannot be proven.

## Skill conflict resolution

The strictest applicable architecture rule wins. In particular:

- no new partial file as a final boundary,
- no service location in core/runtime behavior,
- no inner-to-outer project reference,
- no architecture claim based only on DI resolution,
- no old/new duplicate production path left without a bounded removal plan.
