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
