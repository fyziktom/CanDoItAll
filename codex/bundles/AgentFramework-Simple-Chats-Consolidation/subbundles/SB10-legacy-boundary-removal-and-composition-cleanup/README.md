# SB10 — Legacy boundary removal and composition cleanup

## Status

- Completed — CP4 Pass
- Stage: cleanup
- Proof tier: Governed

## Objective

Remove the three old Modules.LlmChats projects/namespaces and all duplicate route/navigation/DI/composition residue after every consumer uses the new MAF owners.

## Owned Requirements

- ASCC-002
- ASCC-006
- ASCC-007
- ASCC-008
- ASCC-009
- ASCC-010
- ASCC-011
- ASCC-012
- ASCC-013
- ASCC-014
- ASCC-015
- ASCC-016
- ASCC-033
- ASCC-034
- ASCC-043
- ASCC-044
- ASCC-045
- ASCC-046
- ASCC-050

## Prerequisites

- SB09
- CP3 Pass

## Current Source Anchors

- repo://src/App/CanDoItAll.Composition/
- repo://src/App/CanDoItAll.Web/
- repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs
- repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/
- repo://src/Modules/CanDoItAll.Modules.LlmChats/
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Ui/
- repo://CanDoItAll.slnx

## Explicit Non-Goals

- Do not remove /chats redirect compatibility.
- Do not rename API routes/tables/scopes/migrations/transfer identity.
- Do not add a permanent forwarding assembly.
- Do not run Stable/full Playwright.
- Do not refactor unrelated MAF/Infrastructure dependencies.

## Implementation Steps

1. Refresh the CP0 caller inventory and require zero new old-namespace/project callers.
2. Cut Web API adapters/contracts/mappers to Core/Application target namespaces with serialized behavior unchanged.
3. Cut App.Composition, hosted dispatcher, options, ModuleAssemblies, UI authorization, and service registrations to target owners.
4. Cut AppDbContext configuration scanning and PostgreSQL migration project reference to SimpleChats.Persistence.
5. Update all unit/component/integration/Playwright test project references/namespaces and solution grouping.
6. Remove obsolete LlmChats full page/navigation/assembly markers and duplicate registrations.
7. Delete the old three project directories and solution entries after caller count reaches zero.
8. Run source guards for CanDoItAll.Modules.LlmChats, old csproj paths, duplicate shell/hosted/usage source registrations, forbidden references, duplicate avatar-selector implementations, and retained inline Agent avatar markup.
9. Run API/SSE/security/migration/transfer/component/composition focused suites and builds.
10. Refresh CodeAnalytics dependency/cycles and run CP4 architecture gate.

## Acceptance Criteria

- [ ] Zero production old namespace/project reference.
- [ ] Old project directories/solution entries are gone.
- [ ] No duplicate DI/navigation/shell/hosted registration.
- [ ] HTTP/table/scope/migration/transfer compatibility passes.
- [ ] /chats redirect remains.
- [ ] Exactly one reusable avatar selector remains and both Agent/Simple Chat editors consume it.
- [ ] CP4 no-new-cycle/architecture gate Pass.

## Validation Depth

- Proof tier: Governed.
- Critical cutover: yes; final source/project/composition truth depends on zero legacy callers.

Governed cutover proof with exhaustive caller/DI/source guards, build/test transcripts, API/schema invariants, CodeAnalytics, checksums, and architecture gate.

## Focused Test Selection

Workspaces:

- tests/Solutions/CanDoItAll.Tests.Unit.slnx
- tests/Solutions/CanDoItAll.Tests.Components.slnx
- tests/Solutions/CanDoItAll.Tests.Integration.slnx

Required:

- SimpleChatArchitectureBoundaryTests
- LlmChatBackendCompositionTests
- LlmChatUiCompositionTests
- ConversationShellRegistrationTests
- LlmChatsApiIntegrationTests
- LlmChatApiHardeningIntegrationTests
- LlmChatsTurnApiIntegrationTests
- DatabaseMigrationIntegrationTests
- LlmChatPersistenceIntegrationTests

Expected discovery: non-zero per selector. Source guards must report zero old production callers and exactly one registration per owned role.

## Invalidation And Broad-Gate Decision

Stable/Playwright forbidden. Reopen on any composition/project/namespace/API/migration/route/navigation/registration/solution/test reference change.

## UI Composition Contract

No new visual design. Canonical Agent page, Simple Chats tab, scoped dashboard, floating shells, and /chats redirect stay unchanged.

## C# Architecture Impact

Completes the contract phase of expand-migrate-contract and proves no legacy dumping-ground assembly remains.

## Boundary Ownership

Target MAF libraries and Agent module/composition roots are sole owners.

## Dependency Direction

Enforce architecture/02-csharp-dependency-direction.md with zero old-project edges.

## Pattern Decision

Bounded compatibility route only; no permanent assembly facade/service locator/reflection.

## Testability Contract

Direct target-owner tests remain runnable after old assembly deletion; a source guard fails if any new old caller appears.

## Partial Class Policy

No partial added during cleanup. Touched legacy partial responsibility must remain reduced.

## Architecture Proof Required

Before/after full ProjectReference/caller/DI graphs, direct target owner proof, zero old callers, builds/tests, CodeAnalytics cycles, architecture gate.

## Progression Gate

- CP4 Pass unlocks the frozen final candidate in SB11.

## Reopen Triggers

- old namespace/path returns;
- compatibility assembly retained;
- duplicate registration;
- API/schema/route behavior drift;
- new/enlarged cycle;
- target owner not directly testable.

## Covered Inputs

- Raw request: LlmChats projects must no longer live under Modules naming/grouping; Agent module remains thin.
- Requirements ASCC-002, ASCC-006–016, ASCC-033–034, ASCC-043–046, ASCC-050.

## Exact Source References

- C:\repositories\CanDoItAll\src\App\CanDoItAll.Composition
- C:\repositories\CanDoItAll\src\App\CanDoItAll.Web
- C:\repositories\CanDoItAll\src\Foundation\CanDoItAll.Migrations.PostgreSql
- C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.LlmChats
- C:\repositories\CanDoItAll\CanDoItAll.slnx

## Deliverables

- Zero legacy projects/namespaces/callers, clean composition/API/migrations/solution/tests, one registration per role, preserved compatibility redirect.

## Dependency Impact

- SB11 can freeze only after this cutover; any old caller, duplicate DI, or compatibility drift invalidates final tests/browser evidence.

## Acceptance Checklist

- All Acceptance Criteria above pass with zero old production references and green API/schema/composition-focused checks.

## Proof Required

- proof/SB10/manifest.md with exhaustive caller/reference/DI guards, builds/tests, API/schema invariants, CodeAnalytics cycle diff, deletion/hashes, architecture gate.

## Browser Validation Logging

- No new design proof. Reuse CP3 browser evidence only when all route/component/DI/browser inputs hash-identically.
- If cleanup changes assembly discovery, navigation, route, shell, or UI registration, reopen SB08/SB09 and repeat their named 1600x1000 normal/open-overlay proof before CP4.
