# Assumptions And Risks

## Assumptions

- Execution begins from the current `simple-chats` lineage. If the starting commit differs from `a8e3f87e...`, SB01 must classify every intervening change before another work unit starts.
- PostgreSQL 16 remains the integration authority and migrations continue through `CanDoItAll.Migrations.PostgreSql`.
- Existing test seams (`TimeProvider`, deterministic provider substitutes, profile switch barriers, API host, and PostgreSQL fixture) remain available; no live external provider is needed to prove application behavior.
- The source dependency graph remains the repository default and CI pins the sibling Components/FileTools commits.
- A real remotely exposed deployment still enables the canonical bearer authorization configuration; this bundle does not invent tenant ownership.

## Critical Path Risks

- The critical path is transaction/lifecycle first, durable evidence/retention second, and focused architecture/SSE proof third; skipping or weakening an upstream gate invalidates every later result.

| Risk | Consequence | Mitigation / stop rule |
| --- | --- | --- |
| Scope becomes a general chat redesign. | Delayed closure and avoidable compatibility risk. | Each work unit names exact owners; no UI, agent, RAG, moderation, or project-boundary extraction. |
| Concurrency tests are timing-only. | Flaky green proof can miss CAS, lease, or cancellation races. | Use deterministic barriers/two contexts and assert durable database state plus side-effect counts. |
| Provider task is cancelled but not drained. | Hidden orphaned work/unobserved exceptions. | SB04 requires token cancellation and awaited task observation on every exit. |
| Public audit/editor fixes leak new secrets. | Provider identity, correlation, prompt, or raw exception exposure. | Allowlist DTO fields, use read/manage scopes explicitly, and test distinctive sentinel secrets. |
| Retention eviction introduces lost wakeups. | SSE latency or missed terminal delivery. | Durable polling/journal remains authority; prove eviction races and terminal replay, never rely on process-local signal correctness alone. |
| Repeatable-read replay harms throughput. | Long snapshots or connection pressure. | Keep transaction read-only, bounded, and short; compare with a single-statement implementation if profiling shows pressure. |
| Worker concurrency breaks per-conversation serialization. | Double dispatch or transcript corruption. | Durable claim/active-turn invariants remain authoritative; prove cap and no two active operations per conversation. |
| Operation timeouts classify paid completion ambiguously. | Unsafe redispatch or wrong terminal result. | After provider dispatch, ambiguous outcomes become `RecoveryRequired`; never auto-redispatch without evidence. |
| High-water schema/transfer diverge. | Cursor regression after restart/import. | Migration, snapshot, transfer, replay, and pending-model checks close together before SB09. |
| Broad gate runs repeatedly during development. | Slow feedback and unclear evidence. | Focused discovery/tests per work unit; one broad gate only in SB10 at the frozen checkpoint. |
| Current CI changes during execution. | Local proof and matrix prove different graphs. | Pin final source and sibling dependency commits; any workflow/build-graph change reopens SB10. |

## Validation Risks

- A test filter can silently drift to zero after namespace/layout changes; every work unit lists before execution and records exact discovery.
- Unit substitutes cannot prove PostgreSQL isolation, migration, transfer, model binding, authorization, or SSE transport; the owning work units require real boundaries.
- Status/count/file-existence evidence is insufficient for Behavioral/Governed work; positive and meaningful negative state evidence is mandatory.
- Broad-gate evidence from multiple commits or repeated subsets is not composable; SB10 owns one frozen run.

## Reopen Triggers

- Source/test/filter/solution drift reopens the owning proof and every dependent checkpoint.
- API/auth/error/DTO drift reopens SB02 and affected host/SSE/final proof.
- Lifecycle/provider/lease/recovery drift reopens SB03/SB04 and all downstream evidence.
- Schema/repository/migration/transfer drift reopens SB05-SB10.
- DI/build/source-dependency/CI workflow drift reopens SB07/SB09/SB10.
- Any raw secret in logs/API/proof is an immediate release stop and reopens the security owner.

## Explicit Non-Blockers

- The obsolete Spreadsheet package is not a blocker under current source mode.
- The absence of live OpenAI/Azure/Ollama credentials is not a blocker; deterministic provider-boundary substitutes are the intended backend proof.
- No UI or browser evidence is required because no browser-visible surface is in scope.

## Deferred Items With Owners

| Deferred item | Reason | Future owner |
| --- | --- | --- |
| Conversation-create idempotency | Safe key namespace depends on deployment/client identity; current API explicitly warns against blind retry. | Deployment/identity or future public-client bundle |
| UI/chat component integration | User explicitly requested a separate refactor bundle. | Future chat-components/UI bundle |
| Organization/per-user ownership | No such aggregate exists in current profile-local product boundary. | Deployment/security architecture |
| Moderation, retrieval/RAG, external channels | Separate product capabilities, not required for ordinary backend chat correctness. | Dedicated future bundles |
| Live provider certification | Environment/credential concern; core proof uses real adapters with deterministic transport substitutes. | Operational certification lane |
