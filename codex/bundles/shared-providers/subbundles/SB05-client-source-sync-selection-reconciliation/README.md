# SB05 — Client source HTTP, trusted URI policy, selection, sync, and reconciliation

State: `LOCKED`  
Proof tier: `Governed`  
Depends on: `SB02, SB03, SB04`  
Next on pass: `SB06`

## Objective

Implement the client-side shared source service, safe catalog HTTP client, conditional synchronization, selection, stable imports, identity pinning, and non-destructive availability behavior.

## Observable outcome

A client can register one central/EGCP source, discover and select publications, and maintain stable local imports across updates, outages, unpublish, and recovery.

## Inputs and current-state anchors

- Bundle root execution contract and architecture documents.
- Current repository state, not only the prepared SHA.
- Relevant source/test impact maps.
- Completed proof and handoff from every dependency.
- Current mandatory SharedInfo skills.

## Scope

- Implement SharedProviderSourceUriPolicy with canonical base path, scheme/TLS/private-network rules, connection-time destination validation, and redirect policy.
- Implement source HttpClient catalog call with token secret resolution, access-context propagation where present, ETag/If-None-Match, bounded response, content type, schema and source identity validation.
- Implement source create/edit/test/enable/disable application service.
- Implement discover/selection command and deterministic reconciliation transaction.
- Create linked provider.candoitall-shared profiles for selected publications.
- Preserve local alias/enabled intent and stable provider ID.
- Update source endpoint/secret reference consistently for imports/effective profiles.
- Implement 304 no-op, idempotent repeat sync, transient failure, auth failure, authoritative missing/unpublished, reappearance, retirement, and source identity mismatch.
- Notify provider profile commit observers only after successful transaction.
- Add service and PostgreSQL/HTTP integration tests.
- No UI yet.

## Out of scope

- No Razor source dialog.
- No local MAF invocation yet.
- No background scheduler beyond an explicit safe service seam.
- No automatic failover across sources.

## Implementation sequence

1. Use Http implementation behind ISharedProviderCatalogClient; Workspace does not construct HttpClient.
2. Resolve one source token reference through existing secret resolver.
3. Preserve reverse-proxy base path in URI joins.
4. Pin remote source identity after first trusted success; require explicit reset on mismatch.
5. Only a successful authoritative catalog may mark unseen selected imports missing.
6. Use ETag 304 without rewriting imports or observer churn.
7. Retirement/deletion follows provider reference policy.
8. Persist sanitized snapshot only after strict validation.
9. Record source status with sanitized messages.
10. Add no-secret/no-content persistence/log tests.

## C# Architecture Impact

This subbundle is architecture-significant. Re-read
`architecture/00-csharp-current-state-inventory.md` through
`architecture/04-csharp-testability-plan.md`, update the affected checkpoint, and stop rather
than use a boundary workaround.

## Boundary Ownership

Workspace owns source/import use cases and transactions. Http integration owns safe catalog transport. Security owns secret values.

## Dependency Direction

Workspace uses ISharedProviderCatalogClient from Abstractions. Http implementation is wired by Composition. No direct Web/UI dependency.

Record before and after `ProjectReference`/namespace direction even when no reference is
expected to change. A no-change result is still evidence.

## Pattern Decision

Safe outbound client, identity pinning, deterministic reconciliation/state machine, transaction boundary.

Do not introduce an adjacent alternative pattern without reopening the owning ADR and
recording why the selected pattern failed.

## Testability Contract

Scripted DNS/redirect/HTTP handlers and real PostgreSQL services; fixed catalog snapshots and clocks.

Every new behavior needs one realistic positive proof and one meaningful negative proof. Test
existence, file counts, status codes alone, or mocked self-assertions do not prove behavior.

## Partial Class Policy

New source/reconciliation services, not WorkspaceService partial expansion.

A large partial or monolithic file is a gate failure unless the architecture review documents
a narrow unavoidable reason.

## Architecture Proof Required

- URI/network policy matrix.
- Source schema/identity/ETag validation.
- Selection and stable ID transaction evidence.
- Idempotent/no-op observer behavior.
- Outage versus authoritative missing distinction.
- Reappearance and retirement.
- Secret reference stored once and redaction scan.

## Test selection

| Topic | Owning project/lane | Stable filter | Planned expected discovery | Selection reason |
| --- | --- | --- | ---: | --- |
| `SharedProviderSourceUriPolicyTests` | `tests/Solutions/CanDoItAll.Tests.Unit.slnx` | `FullyQualifiedName~SharedProviderSourceUriPolicyTests` | 18 | Covers URI, TLS, network, redirect and DNS policy. |
| `SharedProviderReconciliationTests` | `tests/Solutions/CanDoItAll.Tests.Unit.slnx` | `FullyQualifiedName~SharedProviderReconciliationTests` | 22 | Covers idempotent state transitions and local ownership preservation. |
| `SharedProviderSourceSyncIntegrationTests` | `tests/Solutions/CanDoItAll.Tests.Integration.slnx` | `FullyQualifiedName~SharedProviderSourceSyncIntegrationTests` | 16 | Covers real HTTP/secret/PostgreSQL synchronization and observer behavior. |

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

- One source can import multiple publications with one credential reference.
- Repeated sync creates no duplicates.
- Local provider ID/alias/enabled intent survive remote updates.
- 304 is a true no-op.
- Outage does not delete/retire imports.
- Source identity mismatch blocks.

## Negative proof

- userinfo/query/fragment/non-HTTP URI rejected.
- Unapproved private HTTP/redirect/DNS destination rejected.
- Invalid/missing scope and unsupported schema fail safely.
- Catalog duplicate publication/model IDs rejected.
- Temporary failure cannot mark imports missing.
- Forged remote fields cannot write secrets/internal IDs.

## Semantic invariants

- Only successful authoritative catalog absence changes remote availability.
- Source identity and credential are canonical at source level.
- Sync never silently substitutes or deletes a provider.

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
`SB06`, and update the owning review.

On failure, keep downstream work locked. Do not call a missing proof a residual risk.

## Reopen triggers

- Current safe HTTP helper should own URI policy instead of a new implementation.
- Source edit materialization invariant chosen in SB02 proves insufficient.
- Catalog contract changes after SB04 capability freeze.

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
