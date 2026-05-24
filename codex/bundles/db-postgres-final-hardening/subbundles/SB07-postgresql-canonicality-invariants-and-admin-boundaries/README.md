# SB07 — PostgreSQL canonicality invariants and admin boundaries

## Status

Prepared.

## Objective

Encode database source-of-truth rules so future agents cannot accidentally drift runtime and persisted profile state.

## Covered Inputs

- User requested review of what Codex fulfilled and skipped.
- User requested removal of DB bottlenecks left from SQLite-era protection.
- User requested preserving canonical database source-of-truth.

## Prerequisites

- Work from branch `db-remove-sqlite`.
- Do not reintroduce SQLite runtime provider, migrations, or UI.
- Keep code comments in English.
- Read `codex/skills/bundles/candoitall-bundle-execution/SKILL.md` before implementation.

## Exact Source References


- `repo://src/CanDoItAll.Infrastructure/ControlPlane/**`
- `repo://src/CanDoItAll.Infrastructure/Persistence/**`
- `repo://src/CanDoItAll.Modules.Workspace/Database/DatabaseProfileWorkspaceService.cs`
- `repo://src/CanDoItAll.Web/Components/Layout/MainLayout.DatabaseProfiles.cs`


## Deliverables


1. Add architectural tests/grep tests that normal runtime services do not inject `IProfileAppDbContextFactory`.
2. Allow profile-specific factory only in bootstrap/schema/transfer/explicit maintenance classes.
3. Add tests for runtime profile vs pending restart profile in service/API/UI DTOs.
4. Document source-of-truth rules in development runtime docs.
5. Ensure `DatabaseSwitchNotificationService` naming/events do not imply runtime hot switch if only restart is supported.


## Dependency Impact

This subbundle affects downstream trust in throughput/canonicality proof.

## Validation Depth

Critical where indicated. Use source audit, focused tests, broad validation when possible, and anti-stub checks.

## Implementation Steps


1. Add architectural tests/grep tests that normal runtime services do not inject `IProfileAppDbContextFactory`.
2. Allow profile-specific factory only in bootstrap/schema/transfer/explicit maintenance classes.
3. Add tests for runtime profile vs pending restart profile in service/API/UI DTOs.
4. Document source-of-truth rules in development runtime docs.
5. Ensure `DatabaseSwitchNotificationService` naming/events do not imply runtime hot switch if only restart is supported.


## Scope Exceptions

None unless explicitly documented in proof.

## Do Not Do

- Do not hide failures behind focused tests only.
- Do not claim throughput improvement without either numeric benchmark or clearly stated limitation.
- Do not introduce new non-canonical DB source-of-truth.


## Acceptance Checklist


- [ ] Runtime hot path uses canonical pooled factory.
- [ ] Profile-specific factory has approved call sites only.
- [ ] UI/API never labels pending activation as active runtime.
- [ ] Documentation states canonical DB invariant.


## Proof Required


- `proof/SB07/manifest.md`
- architecture test transcript
- UI/API service tests


## Browser Validation Logging

N/A unless Data Sources UI or runtime/pending activation display changes.

## Progression Gate

This subbundle is complete only when proof artifacts are written under `proof/` and downstream subbundles can rely on its claims.

## Suggested Agent Prompt

Execute this subbundle exactly. Preserve canonical runtime DB invariants and create artifact-backed proof before moving to the next subbundle.
