# SB01 — MAF 1.18 Package and Compile Migration

## Status

Proven

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

Passed on 2026-08-20.

- Version diff: stable `1.17.0` to `1.18.0`; preview
  `1.17.0-preview.260804.1` to `1.18.0-preview.260818.1`.
- Resolved package graph: all direct consumers and both test projects resolve
  `Microsoft.Agents.AI`, `.Abstractions`, `.OpenAI`, and `.Workflows` at `1.18.0`;
  A2A and Hosting packages resolve at `1.18.0-preview.260818.1`.
- Compile breaks: none in product source. The first unit build correctly failed because
  `CanDoItAll.slnx` excludes tests and their assets still referenced 1.17; restoring the
  unit project replaced that stale graph.
- Breaking rename action: none; no old session-isolation symbol exists in active source.
- Package warnings: none after the test-project restore. No downgrade or mixed-version
  warning remains.
- Builds: Maf, Hosting, and Workflows.MafAdapter passed in Release; the unit project
  passed in Debug with zero warnings and zero errors.
- Focused tests: `MafRuntimeArchitectureServicesTests` 52,
  `MafWorkflowAdapterIsolationTests` 4, `MafWorkflowEventNormalizerTests` 3,
  `A2ARemoteAgentToolFactoryTests` 3, `AgentA2AHostCardFactoryTests` 2,
  `AgentFrameworkHostingServiceCollectionTests` 5, and `AgentA2AMetadataTests` 4;
  73/73 passed.
- Documentation: no maintained version statement still names 1.17; the central props file
  remains the package-version source of truth.
- Files changed: `src/MAF/MicrosoftAgentFramework.Packages.props` and execution bundle
  state. The bundle scanner was repaired to enumerate active Git files once per scan
  category instead of repeatedly traversing ignored build trees.
- Blockers/deviations: the existing Web process locks Release output, so the broad unit
  project used Debug. Product consumer builds used Release. The prescribed upgraded
  scanner passes with no errors, warnings, or findings.
