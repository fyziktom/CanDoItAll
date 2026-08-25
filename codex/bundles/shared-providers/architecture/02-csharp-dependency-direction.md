# C# dependency direction

Required by the C# architecture bundle guard.

## Prepared current graph

Relevant current references include:

- `CanDoItAll.Modules.Workspace` -> AgentFramework.Models, SharedKernel, Infrastructure,
  Projects, Security, AppComponents.
- `CanDoItAll.Modules.AgentFramework` -> Workspace plus MAF/Core/Usage/Runtime/UI surfaces.
- `CanDoItAll.Web`/Composition -> product modules and API infrastructure.
- inner `CanDoItAll.AgentFramework.Providers` -> Models and provider-pipeline abstractions.
- `CanDoItAll.AgentFramework.Maf` -> provider contracts/models and provider SDK integration.

## Intended graph

```text
SharedKernel
    ^
    |
SharedProviders.Abstractions
    ^                         ^
    |                         |
Workspace module        SharedProviders.Http
    ^                         ^
    |                         |
AgentFramework module   Composition/Web
    ^                         |
    |_________________________|
              |
             Web
```

More precisely:

1. Abstractions is inward and implementation-neutral.
2. Http points inward to Abstractions.
3. Workspace points inward to Abstractions, never outward to Http.
4. Web/Composition references both and registers implementation against abstraction.
5. AgentFramework module may reference Workspace and maps effective profiles into inner MAF
   types as it already does.
6. Inner MAF projects gain no Workspace/Http/Web reference.
7. Migrations reference entity configurations through the existing model registry; Foundation
   does not reference Workspace.

## Required before/after proof

SB00 and every project-reference-changing subbundle must record:

- current `.csproj` ProjectReference table;
- intended role of each affected project;
- every added/removed reference and why;
- CodeAnalytics project/namespace/type cycle results when available;
- a direct build of each changed project;
- proof that Abstractions contains no forbidden namespace/package;
- proof that Http has no UI/EF/Web reference;
- proof that MAF/Core has no new outer reference.

## Cycle resolution rule

Do not solve a cycle with:

- `Common`;
- `object`/dynamic contracts;
- reflection;
- static service locator;
- duplicated DTOs;
- moving EF entities into SharedKernel;
- making Workspace reference the HTTP implementation.

Extract the smallest stable port or move wiring to Composition.

## Expected project changes

Subject to SB00 validation:

- add `CanDoItAll.SharedProviders.Abstractions.csproj`;
- add `CanDoItAll.SharedProviders.Http.csproj`;
- Workspace adds Abstractions reference;
- Web or Composition adds Abstractions and Http references;
- test projects add only the implementation/contract references they directly test;
- root solution and test solution inventories include the new production/test projects;
- no new reference from AgentFramework.Models, AgentFramework.Providers, or AgentFramework.Maf
  to Workspace/Integration/Web.

## SB00 validated baseline

CodeAnalytics before/after snapshots recorded 11 scoped projects, 23 direct product references,
and zero project-level cycles. The planned edges above are therefore executable as written. The
only reported cycles are two pre-existing namespace/module pairs inside Infrastructure and
Modules.AgentFramework plus one outer/nested type pair in the image-generation tool. They are
explicitly classified baseline risks, not permission to reverse a project dependency.

## SB01 realized dependency graph

- Added production project: `CanDoItAll.SharedProviders.Abstractions`.
- Added production edge: `CanDoItAll.Web -> CanDoItAll.SharedProviders.Abstractions`.
- Added test-only edges from the owning unit and integration test projects.
- Abstractions has zero outgoing project/package edges.
- No AgentFramework, Workspace, Infrastructure, Security, Migration, Composition, or UI project
  gained an SB01 production edge.
- CodeAnalytics snapshot `snap-20260824213007-c65710b4` reports 12 scoped projects, 24 direct
  product references, and zero project-level cycles. The same two pre-existing module cycles
  and one nested-type cycle remain unchanged.

The graph therefore matches the authorized SB01 delta exactly. SB02 may add the Workspace inward
reference; any other product edge requires reopening the owning architecture checkpoint.

## SB02 realized dependency graph

- Added exactly `CanDoItAll.Modules.Workspace -> CanDoItAll.SharedProviders.Abstractions`.
- Added no production project and no other product edge.
- Abstractions still has zero outgoing package/project references.
- Foundation/Migrations has no Workspace reference; inner AgentFramework projects have no new
  outer reference; Workspace has no SharedProviders.Http reference.
- CodeAnalytics snapshot `snap-20260824231242-d9fc36b9` reports 12 scoped projects, 25 direct
  product references, and zero project-level cycles. The two baseline module cycles and one
  nested-type cycle are unchanged.

The 24-to-25 delta matches the authorized CP-02 graph exactly.

## SB06 realized dependency graph

- Added no production project and no product `ProjectReference` edge.
- Inner AgentFramework Models, Providers, and MAF retain no Workspace, SharedProviders.Http, Web,
  or UI implementation reference.
- Workspace still depends only on SharedProviders.Abstractions; Http still depends only on
  Abstractions; Composition remains the concrete implementation boundary.
- The normalized before/after selected-reference audit reports zero delta.
- Force-refreshed snapshot `snap-20260825100508-300644c7` reports 14 scoped product projects,
  34 direct references, zero project cycles, and unchanged governed two module plus one nested-type
  cycles.

The CP-04 runtime projection therefore closes without a boundary workaround or graph reopen.
