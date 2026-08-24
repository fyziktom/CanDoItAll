# SB07 — Backend checkpoint and three-instance Docker proof

State: `LOCKED`  
Proof tier: `Governed`  
Depends on: `SB06`  
Next on pass: `SB08`

## Objective

Prove the complete backend through one central and two client CanDoItAll containers, real PostgreSQL, real HTTP, deterministic upstream, authorization, streaming, synchronization, failure, and recovery before UI.

## Observable outcome

The backend checkpoint gate passes with machine-readable scenario artifacts; only then is SB08 unlocked.

## Inputs and current-state anchors

- Bundle root execution contract and architecture documents.
- Current repository state, not only the prepared SHA.
- Relevant source/test impact maps.
- Completed proof and handoff from every dependency.
- Current mandatory SharedInfo skills.

## Scope

- Create repository-owned deterministic upstream test-support host/container.
- Create dedicated compose.shared-providers.e2e.yaml reusing one app image for central/client-a/client-b.
- Use separate databases/data roots/host identities.
- Create non-production E2E orchestrator using canonical application services, not direct SQL or production bypass endpoints.
- Generate ignored ephemeral credentials and safe handoff paths.
- Build app image once and start full topology.
- Seed central published/unshared text/image fixtures and two independent clients.
- Run all backend acceptance scenarios from requirements/03-acceptance-criteria.md.
- Capture upstream request assertions, central invocation metadata, source/import/local IDs, ETag behavior, container health and sanitized logs.
- Run backend architecture/security review.
- Keep stack state available during debugging, but final leave-running contract is SB12.
- Do not implement new UI.

## Out of scope

- No Playwright.
- No final stable aggregate.
- No live/paid provider.
- No SharedInfo snapshot.

## Implementation sequence

1. Pin deterministic upstream behavior and image tag/source hash.
2. Use a dedicated Docker network and explicit health dependencies.
3. Initialize databases safely and refuse production-looking reset targets.
4. Configure instances with canonical services and existing vault/token services.
5. Ensure central/client data are independent and inspectable.
6. Run scenarios individually with failure names, not one opaque pass/fail.
7. Measure first streaming chunk before completion and verify disconnect cancellation.
8. Capture access-context header at central and prove upstream lacks it.
9. Stop/restart/unpublish/re-publish central and sync both clients.
10. Scan database/log/artifacts for known prompt/secret canaries.
11. Update reviews/backend-checkpoint-gate.md with PASS/FAIL.

## C# Architecture Impact

This subbundle is architecture-significant. Re-read
`architecture/00-csharp-current-state-inventory.md` through
`architecture/04-csharp-testability-plan.md`, update the affected checkpoint, and stop rather
than use a boundary workaround.

## Boundary Ownership

Tests/Support and tools own fixtures/orchestration. Product code is changed only to fix proven backend defects, followed by owning focused tests.

## Dependency Direction

E2E support may reference Composition/application services but must not become a production dependency. Product app image remains the normal Web image.

Record before and after `ProjectReference`/namespace direction even when no reference is
expected to change. A no-change result is still evidence.

## Pattern Decision

Black-box system test with canonical setup tool and deterministic upstream.

Do not introduce an adjacent alternative pattern without reopening the owning ADR and
recording why the selected pattern failed.

## Testability Contract

Real app containers and PostgreSQL; deterministic controls for delay/errors/tools/images; machine-readable scenario report.

Every new behavior needs one realistic positive proof and one meaningful negative proof. Test
existence, file counts, status codes alone, or mocked self-assertions do not prove behavior.

## Partial Class Policy

No production test hooks in runtime partials. Test support is isolated.

A large partial or monolithic file is a gate failure unless the architecture review documents
a narrow unavoidable reason.

## Architecture Proof Required

- Compose config and image reuse.
- Three app health/identity/database evidence.
- Scenario-results.json with every acceptance scenario.
- Streaming timing/cancellation.
- Access-context central-only capture.
- Secret/content scan.
- Backend architecture/security gate PASS.

## Test selection

| Topic | Owning project/lane | Stable filter | Planned expected discovery | Selection reason |
| --- | --- | --- | ---: | --- |
| `SharedProviderBackendCheckpointIntegrationTests` | `tests/Solutions/CanDoItAll.Tests.Integration.slnx` | `FullyQualifiedName~SharedProviderBackendCheckpointIntegrationTests` | 10 | Focused pre-Docker regression for backend checkpoint contracts. |
| `SharedProviderMultiInstanceE2E` | `tools/SharedProviders E2E orchestrator` | `scenario-set:backend-checkpoint` | 19 | Required real central plus two client system proof. |

Before running a test topic:

1. build the owning production/test assembly;
2. run `--list-tests` when it is a .NET test lane;
3. compare actual discovery with the planned count;
4. update the planned count only before execution and with a written implementation-based
   reason;
5. reject zero discovery;
6. record transcript and counts in `proof/proof-manifest.json`.

Do not run an unfiltered project or broader lane unless this subbundle explicitly owns it.

## Acceptance criteria

- All backend acceptance criteria pass.
- Central and both clients are independent.
- No direct SQL fixture mutation or production bypass.
- No live external call.
- UI gate is explicitly unlocked only on PASS.

## Negative proof

- Wrong scopes, unknown model, built-in tools, invalid context, source mismatch, unpublish, outage, timeout, rate limit, upstream error and cancellation all proven.
- Personal provider does not mask shared failure.
- Canary prompt/token absent from logs/audit/catalog.

## Semantic invariants

- UI remains locked until real multi-instance proof passes.
- No E2E secret/content enters tracked artifacts.
- One app image is reused across all CanDoItAll containers.

## Evidence artifacts

At minimum:

- completed `proof/proof-manifest.json`;
- command transcripts under `proof/transcripts/`;
- changed-file inventory;
- architecture/reference artifacts;
- focused behavior artifacts;
- completed `SESSION-HANDOFF.md`;
- updated root `STATUS.md` and traceability rows.

## Progression gate

Pass only when every acceptance criterion, architecture assertion, focused build/test, and
negative proof is backed by an artifact. On pass mark this subbundle `DONE`, unlock only
`SB08`, and update the owning review.

On failure, keep downstream work locked. Do not call a missing proof a residual risk.

## Reopen triggers

- Any backend scenario fails.
- Compose proof uses fewer than three CanDoItAll app instances.
- Fixture bypasses canonical services.
- Architecture/security gate fails.

## Execution checklist

- [ ] Current branch/commit/worktree captured.
- [ ] Mandatory skills loaded.
- [ ] Bundle and subbundle readiness validated.
- [ ] Dependencies are `DONE`.
- [ ] Before architecture/reference evidence captured.
- [ ] Scope implemented without widening.
- [ ] Affected production projects built.
- [ ] Test discovery recorded and nonzero.
- [ ] Focused positive/negative tests passed.
- [ ] Security/redaction checks passed where applicable.
- [ ] After architecture/reference evidence captured.
- [ ] Proof manifest completed with artifact hashes.
- [ ] Session handoff completed.
- [ ] Status/traceability/review updated.
