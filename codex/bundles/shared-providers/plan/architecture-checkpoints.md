# Architecture checkpoints

## CP-00: current-state and decision lock

Owned by SB00.

Pass when:

- current project/symbol/runtime paths are verified;
- CodeAnalytics/reference inventory is captured;
- current provider/usage/delete/OpenAPI behavior is characterized;
- preferred target graph is confirmed or narrowly amended;
- no unresolved cycle or duplicate-runtime plan remains.

## CP-01: contracts and context

Owned by SB01.

Result: `PASS` at CodeAnalytics snapshot `snap-20260824213007-c65710b4`.

Pass when:

- protocol/ports are SDK/EF/Web-free;
- public records cannot carry secrets/internal profiles;
- routing IDs and access context have direct tests;
- project-reference graph remains acyclic.

Realized evidence: the SDK/EF/Web-free Abstractions project has zero outgoing references; the
only new production edge is `Web -> Abstractions`; strict protocol/routing/access lanes pass
12/10/10; the graph has 12 projects, 24 direct references, and zero project-level cycles.

## CP-02: persistence ownership

Owned by SB02.

Result: `PASS` at CodeAnalytics snapshot `snap-20260824231242-d9fc36b9`.

Pass when:

- explicit publication/source/import/invocation entities and indexes exist;
- PostgreSQL migration/model snapshot are correct;
- reconciliation state machine preserves local identity;
- new code is cohesive, not appended to large partials;
- concurrency and negative transitions are tested.

Realized evidence: five explicit Workspace entities plus cohesive transitions/services and one
generated PostgreSQL migration are present; exact state/persistence/deletion lanes pass 18/14/6;
two-profile propagation, persisted optimistic rollback, non-destructive reconciliation, metadata-
only audit, typed deletion/transfer blocks, and clean migration are proven. The graph has 12
projects, 25 direct references, zero project cycles, and exactly the authorized
`Workspace -> SharedProviders.Abstractions` delta.

SB04 added operation-aware `ImageCount` to the SB02-owned invocation schema and public usage
projection, invalidating the original CP-02 schema proof. Trust was restored additively by fresh
18/18 state, 14/14 PostgreSQL persistence, 6/6 deletion/reference, 7/7 usage aggregation, and EF
pending-model/no-drift runs. The migration edit is valid only while the amended migration has not
been applied to a durable/non-disposable database; otherwise a new migration is required.

## CP-03: central API

Owned by SB03/SB04.

Result: `PASS_SB04` at CodeAnalytics snapshot `snap-20260825051057-300644c7`.

Realized evidence: SB03 proves explicit eligibility and administrator publication, canonical
sanitized catalog/routing projection, persisted cross-instance cache invalidation, native catalog
and OpenAI models discovery, catalog-read/invoke policies, safe errors, and RFC 9110 ETag/304 in
exact 18/14/10 lanes. SB04 binds every advertised production descriptor to one registered adapter,
re-resolves persisted publication/profile/secret state before dispatch, exposes only the bounded
Chat Completions, Responses, and Images POST surfaces, and proves strict field/tool/capability
allowlists, no-open-proxy routing, incremental SSE, cancellation/disposal, distinct timeouts,
sanitized errors/headers, bounded base64 images, truthful usage, and metadata-only durably
terminalized audit in exact 24/22/12 lanes. Supporting 7/7 aggregation and the restored SB02
18/14/6 plus EF no-drift lanes prove operation-disjoint token/image usage. The real PostgreSQL
application lane uses current Workspace routing, secrets, audit, hosted recovery, and image
target resolution with a deterministic neutral dispatcher rather than a live provider call. The
graph has 14 scoped product projects, 34 direct
references, and zero project cycles; the SB04 delta is the authorized
`Modules.AgentFramework -> SharedProviders.Abstractions` bridge, while Workspace and Web retain no
Http implementation edge.

Pass when:

- sanitized catalog/auth/ETag work;
- relay is adapter-driven and not open proxy;
- compatibility subset is honest;
- streaming, tools, structured output, images, cancellation, errors, and usage are proven;
- no content/secret leakage.

## CP-04: client runtime

Owned by SB05/SB06.

Result: `PASS_SB06`; CP-04 is closed.

SB05 realized evidence: exact URI/network 18/18, reconciliation 22/22, and real HTTP/secret/
PostgreSQL source-sync 16/16 prove canonical URI/private-loopback/TLS policy, per-connection DNS
revalidation, no redirects, source lifecycle, identity pinning/reset, safe ETag/304, idempotency,
stable local identity/intent, authoritative-only missing, non-destructive recovery, replacement
retirement, and post-commit observers. Snapshot `snap-20260825070408-300644c7` retains 14 scoped
projects, 34 direct references, and zero project cycles with no reference delta.

SB06 realized evidence: exact materializer 18/18, runtime projection 16/16, and hybrid selection
10/10 prove the shared connector projects into the existing raw OpenAI and MAF SDK paths while
personal providers coexist. Exact model binding, typed source credentials, hardened network
selection, request-scoped context, sanitized failures, unavailable retention, and no fallback are
enforced. The post-review audio repair fails closed before credential/HTTP dispatch and prevents an
ineligible persisted voice selection from rebinding to a personal provider. Snapshot
`snap-20260825100508-300644c7` retains 14 projects, 34 direct references, zero project cycles, and
no reference delta.

August 25 downstream revalidation: SB07 changed named SB04 Responses wire-contract and relay
operation/capability semantics. Because the earlier SB06 transcripts resolved to Debug assemblies,
the unchanged frozen materializer/runtime/hybrid selections were freshly listed and passed in
genuine Release at 18/18, 16/16, and 10/10. The behavior delta adds no project/reference/public-type
edge or alternate runtime, so `PASS_SB06` and CP-04 remain closed. Evidence:
`bundle://subbundles/SB06-shared-connector-runtime-projection-hybrid-use/proof/architecture/sb04-downstream-invalidation-release-revalidation.md`.

Pass when:

- URI/network policy and source identity pinning work;
- sync is idempotent/non-destructive;
- shared connector projects to existing MAF path;
- personal/shared coexistence and no-fallback behavior are proven;
- no inner-project reverse reference.

## CP-05: backend feature gate

Owned by SB07.

Pass only with the three-app Docker matrix. This checkpoint unlocks UI.

## CP-06: UI

Owned by SB08/SB09.

Pass with component/Playwright/screenshot/accessibility evidence and service-side ownership
enforcement.

## CP-07: external contract and closure

Owned by SB11/SB12.

Pass with synchronized OpenAPI/SharedInfo, one stable aggregate, final clean Docker run, running
stack, and complete traceability.
