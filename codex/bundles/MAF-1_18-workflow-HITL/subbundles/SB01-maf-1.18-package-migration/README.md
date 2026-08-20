# SB01 — MAF 1.18 Package and Compile Migration

## Status

Prepared

## Outcome

Upgrade every active Microsoft Agent Framework dependency to the intended 1.18 stable/preview family and resolve only the compile/runtime adaptations directly required by the release.

## Owned requirements

RQ-001, RQ-002, RQ-003, RQ-004, the compile portion of RQ-005, and the package/documentation portion of RQ-043 and RQ-044.

## Non-goals

- enabling concurrent tool invocation;
- changing tool execution behavior;
- implementing workflow HITL;
- adopting optional 1.18 experiments;
- broad MAF wrapper refactoring;
- upgrading unrelated packages;
- suppressing compile breaks with reflection/dynamic compatibility shims.

## Prerequisites

SB00 passed and its package/code inventory is current.

## Reopen triggers

- IK-01, IK-02, IK-03, IK-04, IK-07, IK-15;
- NuGet resolves a mixed 1.17/1.18 graph;
- a direct package uses a different official target coordinate;
- restore introduces an unexplained downgrade;
- a compile adaptation changes public behavior and therefore belongs in SB02 or later.

## Exact sources and discovery

Primary edit:

- `src/MAF/MicrosoftAgentFramework.Packages.props`

Build consumers from SB00 inventory, including at minimum:

- `CanDoItAll.AgentFramework.Maf`
- `CanDoItAll.AgentFramework.Hosting`
- `CanDoItAll.AgentFramework.Workflows.MafAdapter`
- unit test project
- integration test project when API/hosting references require it.

Search for old breaking symbols:

```text
SessionIsolationKeyProvider
SessionIsolationKeyProviderOptions
AddSessionIsolationKeyProvider
ClaimsIdentitySessionIsolationKeyProvider
```

Search resolved assets for active MAF 1.17 packages after restore.

Review upstream 1.18 API source for any actual compile break before inventing a compatibility layer.

## Implementation boundary

1. Change stable property to `1.18.0`.
2. Change preview property to `1.18.0-preview.260818.1`.
3. Restore.
4. Inspect resolved package versions and downgrades.
5. Resolve compile errors:
   - use new agent-isolation names if the old names are actually referenced;
   - adapt moved/renamed APIs directly;
   - preserve existing behavior;
   - do not add multi-version reflection.
6. Update package/version documentation that explicitly states 1.17.
7. Keep behavior changes out of this subbundle unless compilation cannot be separated; when unavoidable, document and add a focused regression.

## Acceptance criteria

- central version properties contain the target values;
- every active stable MAF package resolves to 1.18.0 or a documented official companion version;
- every active A2A preview package resolves to 1.18.0-preview.260818.1 or a documented official companion version;
- no active MAF 1.17 package remains;
- no package downgrade warning is ignored;
- old isolation provider symbols are absent from active source or migrated correctly;
- all affected projects build;
- public APIs are not hidden behind reflection shims;
- existing workflow/agent architecture isolation tests still pass;
- the diff is reviewable independently from HITL work.

## Proof tier

Standard

## Focused validation

Run after one restore:

```bash
python <bundle>/scripts/check_maf_upgrade.py <repo-root> --mode upgraded
dotnet build src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj --no-restore
dotnet build src/MAF/Common/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj --no-restore
dotnet build src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj --no-restore
dotnet build tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore
```

Run focused tests:

- `MafRuntimeArchitectureServicesTests`
- `MafWorkflowAdapterIsolationTests`
- `MafWorkflowEventNormalizerTests`
- compile-sensitive hosting/A2A tests discovered in SB00.

Expected discovery is the SB00 count floor and greater than zero.

## Invalidation keys

IK-01, IK-02, IK-07, IK-15, IK-16, IK-17.

## Broad-gate decision

No solution-wide test gate. Affected project builds are sufficient at this stage. The full solution build belongs to FG-01.

## Closure record

Not executed.

Record:

- version diff:
- resolved package graph:
- compile breaks:
- breaking rename action:
- package warnings:
- builds:
- focused tests/counts:
- documentation:
- files changed:
- blockers/deviations:
