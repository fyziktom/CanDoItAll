# Decision register

## Locked decisions

| ID | Decision |
|---|---|
| ADR-001 | Ordinary LLM chat is not agent execution. |
| ADR-002 | Product definitions and conversations belong to a new LLM Chats module. |
| ADR-003 | Domain/application and EF persistence are separate projects. |
| ADR-004 | PostgreSQL is the production store; file store remains isolated. |
| ADR-005 | Existing generic transcript engine is reused through a product-owned narrow engine. |
| ADR-006 | Generic production `ILlmConversationService` is not globally registered. |
| ADR-007 | Definition behavior is append-only revisioned; conversations pin a revision. |
| ADR-008 | Operation ID equals generic turn ID for crash reconciliation. |
| ADR-009 | Existing database runtime profile identity is the only profile fence source. |
| ADR-010 | Provider calls and transcript commits form a recoverable saga, not a fake ACID transaction. |
| ADR-011 | UI, concrete context sources, attachments, streaming, and deployments are separate bundles. |
| ADR-012 | Broad tests run only once in the final gate. |
| ADR-013 | LLM Chat thinking effort reuses the existing provider/model capability truth. A definition revision stores a nullable typed override (`null` provider default, explicit `None` disable), API clients receive a sanitized per-model capability projection, and invocation audit records requested/effective effort. No second catalog or per-turn override is allowed. |

## SB00 decisions to resolve from current source

| ID | Question | Allowed outcome |
|---|---|---|
| DEC-001 | canonical organization identity | reuse exact existing type/ID, or profile-local scope with explicit deferral |
| DEC-002 | canonical authenticated API subject identity | reuse exact existing accessor, or no per-user ownership in this bundle |
| DEC-003 | provider profile and capability resolver | reuse the canonical snapshot implementation through provider-neutral contracts; if still located in AgentFramework Core, extract only the read/capability contracts to `AgentFramework.Providers` and update current consumers |
| DEC-004 | API error/authorization convention | reuse current Web adapters and policy registration |
| DEC-005 | EF transaction helper | reuse existing unit-of-work/serializable helper where suitable |
| DEC-006 | operation activity/cancellation helper | reuse only if provider-neutral; otherwise add narrow LLM Chat helper |
| DEC-007 | profile-switch drain/commit coordination | prove current restart lifecycle is sufficient, or integrate a narrow active-operation drain participant into the canonical switch path |
| DEC-008 | thinking-effort contract and request seam | reuse `AgentReasoningEffortLevel`, `ProviderModelThinkingEffortCapability`, and `AgentThinkingEffortPolicy` as provider-neutral truth; add the smallest typed lightweight-invocation setting/property and product-safe option projection without agent execution dependencies or duplicate parsing |

No implementation subbundle may invent an answer before CP0.

## SB00 resolved outcomes

| ID | Resolution | Evidence and consequence |
|---|---|---|
| DEC-001 | Profile-local scope; do not add an organization column in this bundle. | No canonical product tenant/organization owner exists. Agent workspace `Organization` scope is derived from the active database profile ID and is agent-workspace metadata, not a cross-product organization aggregate. Each active PostgreSQL profile already isolates its LLM Chat rows. |
| DEC-002 | No per-user ownership in this bundle. | The API supports optional bearer authorization and can read `NameIdentifier`/`sub`, but there is no canonical local subject directory or ownership accessor. Routes remain authorized; definitions/conversations are profile-local. |
| DEC-003 | Move only `IProviderRuntimeProfileSource` from AgentFramework Core to `AgentFramework.Providers`; retain the canonical snapshot implementation and current provider persistence. Resolve model capability from the existing typed policy. | `Contracts.cs` currently owns the read interface, while `CanonicalProviderRuntimeProfileSnapshotService` implements it. LLM Chats must consume the neutral interface without referencing Core/Modules.AgentFramework. |
| DEC-004 | Use the `/api` route group, conditional global authorization, explicit LLM Chat read/write/execute policies, `ApiEndpointResults`, `ProducesApiErrors`, stable operation names, and ProblemDetails mapping. | `ApiEndpointRouteBuilderExtensions`, `ApiAuthorizationPolicies`, and `MemoryProvidersApi` establish the current convention. No resource ownership is inferred from an optional bearer subject. |
| DEC-005 | Use `SerializableMutationScope` for multi-row aggregate/claim mutations and explicit EF conditional updates for transcript CAS. | It supplies PostgreSQL serializable transactions plus ordered advisory locks and conflict classification. A transcript revision update still requires an affected-row check; a process lock alone is not correctness. |
| DEC-006 | Add the planned narrow LLM Chat operation scope/cancellation registry; do not reuse agent/process/streaming helpers. | Existing helpers either carry agent/process semantics or only manage HTTP streaming task lifetime. None owns persistent paid-operation idempotency and exact turn recovery. |
| DEC-007 | The canonical switch is restart-only and therefore already separates old/new database runtimes; keep generation fences and prove them with a deterministic fake/runtime publication race. | `DatabaseSwitchCoordinator` persists a pending profile and returns `RequiresRestart=true`, `RuntimeChangedInProcess=false`. `DatabaseRuntimeState` is initialized once and rejects an in-process identity replacement. Process shutdown drains/cancels the old host before a new process publishes the new profile. No new switch participant is required. |
| DEC-008 | Reuse `AgentReasoningEffortLevel`, `ProviderModelThinkingEffortCapability`, and `AgentThinkingEffortPolicy`; add an optional typed override to `LlmModelSettings` and a sanitized product provider-options projection. | The Models snapshot proves the types are canonical per-provider/per-model policy. `null` remains provider default and explicit `None` remains distinct. `ProviderBackedLlmInvocationAdapter` translates the typed setting into the existing model-parameter envelope. No duplicate effort parser/catalog is allowed. |

## SB01 concrete file plan

- create `src/Modules/CanDoItAll.Modules.LlmChats/CanDoItAll.Modules.LlmChats.csproj`;
- add cohesive `Definitions`, `Conversations`, `Operations`, and `Common` domain files described in
  `architecture/12-class-and-interface-plan.md`, grouping tiny identifiers by concern;
- add optional init-only `ConversationId` and `TurnId` properties to
  `LlmConversationStartRequest`/`LlmConversationTurnRequest`;
- add optional typed thinking-effort override to `LlmModelSettings` without removing the legacy JSON
  envelope;
- update `LlmConversationService` to select supplied identities or generate GUIDs exactly as today;
- add focused canonical/fingerprint/caller-ID tests; do not add EF, DI activation, or HTTP code.
