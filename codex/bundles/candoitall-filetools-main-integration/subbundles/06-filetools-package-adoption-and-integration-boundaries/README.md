# SB06 FileTools Package Adoption And Integration Boundaries

## Status

- `Completed`

Behavioral proof passed and SB07 was unlocked.

## Objective

- Import validated FileTools artifacts and establish minimal compile-time integration boundaries/adapters without UI or effects.

## Covered Inputs

- N003-N005, N007, N013-N015, N017; R008-R009, R012, R022, R026-R038, R040.

## Prerequisites

- SB05 Pass; SB01 current package hashes trusted.

## Exact Source References

- `repo://NuGet.Config`
- `repo://ExternalPackages`
- `repo://CanDoItAll.slnx`
- `repo://src/Foundation/CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj`
- `repo://src/App/CanDoItAll.Composition/CanDoItAll.Composition.csproj`
- `repo://src/App/CanDoItAll.Web/CanDoItAll.Web.csproj`
- `bundle://architecture/02-csharp-dependency-direction.md`
- `bundle://inputs/01-source-artifacts.md`
- `repo://src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs`.

## Deliverables

- Exact validated FileTools packages copied to local feed with recorded hashes; only required packages referenced per project.
- New `CanDoItAll.FileTools.Integration.Abstractions` and `CanDoItAll.FileTools.Integration` projects at target boundaries.
- Typed semantic scope/session/interaction interfaces and native Storage-to-FileTools provider adapter/catalog/session factory.
- Separate typed known-file interaction and collection browsing contracts; adapter preserves Storage work/completeness/ordering budgets rather than translating only page size.
- One focused declarative feature-registration extension; no module implementation/UI/effect/cache yet.
- Restore/build/static-web-asset/package graph/DI smoke and before/after dependency proof.

## Dependency Impact

- SB07-SB18 depend on package and project boundary correctness; reverse edge invalidates all module work.

## Validation Depth

- Proof tier: `Behavioral`.
- Critical package/dependency foundation.

## Implementation Steps

1. Verify source package hashes against SB01, copy artifacts, and restore from the configured feed.
2. Add projects to solution with minimal references from `architecture/02-csharp-dependency-direction.md`.
3. Implement stable typed contracts before adapters.
4. Map an instrumented fake native provider to FileTools root/path/page/error/cancellation/work-budget/completeness behavior and prove the known-file contract has no browser reference.
5. Add declarative DI extension and composition smoke.
6. Validate static assets and package selectivity.
7. Refresh CodeAnalytics dependencies/cycles and source assertions.

## C# Architecture Impact

- Real project-boundary extraction and external package adapter.

## Boundary Ownership

- Abstractions owns stable contracts only; implementation owns mapping/construction; modules/domain remain absent.

## Dependency Direction

- Exact allowed/forbidden graph in `architecture/02-csharp-dependency-direction.md`.

## Pattern Decision

- PSR-02 Adapter plus narrow catalog/session factory; no application business logic in factory.

## Testability Contract

- Direct adapter/factory tests with fake native drivers; DI/static assets are integration smoke.

## Partial Class Policy

- No partial/nested architecture boundaries.

## Architecture Proof Required

- Package hashes/graph, before/after references, snapshot/dependency/cycle, SDK-free contract assertions, fake adapter tests, declarative registration source.

## Scope Exceptions

- No authorization/handles/endpoints/cache/module UI/save implementation.

## Do Not Do

- Do not add sibling project references, make Infrastructure reference FileTools, reference every renderer package, reference `CanDoItAll.FileTools.Providers.FileSystem` from the main app, erase native work budgets during mapping, conflate interaction with browsing, or return EF/module/storage records from contracts.

## Acceptance Checklist

- [x] Exact validated packages restore; required Abstractions package has no static web assets.
- [x] Boundary projects compile with intended references only.
- [x] Native fake provider maps semantically to FileTools.
- [x] No project cycle, new module cycle, or forbidden edge.
- [x] Composition registration is declarative/tested.

## Proof Required

- Behavioral package/mapping positives, mismatch/unknown/duplicate negatives, restore/build/tests/assets commands, hash/reference/source assertions, CodeAnalytics result.

## Browser Validation Logging

- No product flow; static asset HTTP/manifest smoke may be host-level. Browser UI proof starts SB10.

## Progression Gate

- SB07 enters only after package provenance, mapping semantics, static assets, references, and composition smoke pass.

## Reopen Triggers

- Package/API drift, missing asset, module-specific contract leak, mapping error, or forbidden/cyclic reference reopens SB01/SB06 and all downstream proof.

## Suggested Agent Prompt

```text
Adopt only the validated FileTools packages and establish the two target integration boundaries plus a fake-backed adapter/session composition smoke. Keep Infrastructure and contracts clean, avoid UI/effects/cache, and prove the exact package/reference graph.
```
