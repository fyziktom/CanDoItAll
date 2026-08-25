# Shared Providers Boundary Recovery Bundle

## Purpose

This bundle corrects the module ownership introduced while implementing shared LLM providers on branch `providers-shared`.

The current shared-provider behavior is **not** to be reverted. Publication, discovery, import reconciliation, relay authorization, audit, rate limiting, runtime revision snapshots, secret handling, and fail-closed behavior must be retained. The correction is an ownership and dependency-direction refactor, followed by removal of the older duplicate provider runtime path.

## Starting point

- Repository: `fyziktom/CanDoItAll`
- Branch: `providers-shared`
- Audited HEAD / implementation commit: `fdf1ff9702c376ad0ffd101a34d6bf542c9857d2`
- Audited tree SHA: `ca9681db8764fc14301d9ab0277cf97df711b02b`
- Original shared-provider bundle: `codex/bundles/shared-providers`
- Original SB00-SB06: implemented
- Original SB07: blocked by Docker lifecycle attempts

Codex must verify the branch and HEAD before editing. A newer HEAD is acceptable only when the delta is reviewed and does not alter the decisions in `DECISION-LOCK.md`.

## Architectural verdict

The primary defect was in the original bundle design, not random implementation drift. The bundle explicitly locked Workspace EF as canonical, declared the five shared-provider entities Workspace-owned, and retained the AgentFramework-to-Workspace provider dependency. Codex largely implemented that direction.

The deeper pre-existing cause is that Workspace already owned a second provider stack: provider profiles, adapters, registry, direct HTTP execution, runtime gateway, secrets, health/pricing, and provider transfer. The shared-provider bundle treated this historical placement as an architectural fact instead of debt to remove.

## Required end state

1. Provider management and shared-provider control-plane behavior belong to a dedicated non-Razor project in the AgentFramework module family:
   `src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement`.
2. The new project has no project, namespace, service, entity, or UI dependency on Workspace.
3. Workspace owns no provider profile, provider adapter, provider registry, provider runtime gateway, shared-provider entity/service, or provider transfer implementation.
4. Workspace may retain only a workspace preference such as `DefaultProviderProfileId` as an opaque `Guid?` and a compatibility redirect to `/agents?tab=providers`.
5. `/agents?tab=providers` remains the authoritative provider administration UI.
6. Web API endpoint mapping remains in the Web host, but endpoint implementations consume ProviderManagement ports rather than Workspace services.
7. Runtime inference has one provider path through the AgentFramework/MAF provider drivers. The legacy direct Workspace HTTP execution stack is removed rather than merely relocated.
8. Existing public HTTP routes, shared-provider wire contracts, secret references, persisted IDs, and physical table names remain compatible.
9. Existing shared-provider tables are not dropped or renamed in this corrective bundle.
10. Original SB07 is not resumed until this recovery bundle reaches final acceptance.

## Execution order

Run the subbundles in order:

1. `BR00-freeze-and-characterize`
2. `BR01-create-provider-management-boundary`
3. `BR02-extract-canonical-provider-control-plane`
4. `BR03-relocate-shared-provider-control-plane`
5. `BR04-unify-provider-runtime`
6. `BR05-rewire-ui-api-composition-and-transfer`
7. `BR06-preserve-persistence-and-cleanup`
8. `BR07-architecture-guards-and-focused-gates`
9. `BR08-handoff-to-original-sb07`

Each subbundle creates exactly one `RESULT.md` in its own directory. Do not create proof manifests, duplicated inventories, per-file hashes, or repeated architecture reports.

## Read order for Codex

At session start, read only:

1. `START-CODEX-GPT-5.6-XHIGH.md`
2. `DECISION-LOCK.md`
3. `TARGET-BOUNDARY.md`
4. `EXECUTION-CONTRACT.md`
5. the current subbundle `README.md`
6. the immediately preceding subbundle `RESULT.md`, when present

`ARCHITECTURE-ANALYSIS.md` is supporting rationale. Read it once during BR00; do not repeatedly summarize it.

## Non-goals

- Do not implement the unfinished original SB07 behavior.
- Do not redesign the public shared-provider protocol.
- Do not rename existing provider tables.
- Do not introduce a provider-specific DbContext.
- Do not move EF Core, security, or HTTP concerns into the inner MAF projects.
- Do not refactor unrelated Workspace, Agents, Workbench, or UI behavior.
- Do not run Docker or Podman in this bundle.
