# Pattern selection records

## PS-01 — Immutable snapshot for each agent turn

**Force:** the UI can change while an agent run is active.

**Selected pattern:** immutable snapshot plus digest-bound lease.

**Why:** a run must be reproducible and an approval continuation must not adopt a newer surface.

**Rejected:**

- reading the live registry throughout the run,
- appending tab changes directly to the active provider session,
- replacing the context on approval continuation.

**Proof:**

- run started on Canvas remains Canvas after UI switches to Gantt,
- next run uses Gantt,
- continuation digest remains unchanged.

## PS-02 — Conversation context affinity state machine

**Force:** a long-lived floating chat persists while the user navigates.

**Selected pattern:** explicit conversation binding and transition classifier.

**Why:** transcript continuity and current UI attention are different concerns.

**Rejected:**

- deriving the conversation topic from the last transcript message,
- treating the current global UI registry as per-chat state,
- synthetic user messages on every tab switch.

**Proof:**

- same-project view switch is `ViewChanged`,
- project switch is `SourceEntityChanged`,
- binding revision changes only on an admitted turn or explicit detach,
- no provider call occurs on navigation alone.

## PS-03 — Authority resolver and immutable authority snapshot

**Force:** a UI module can describe a project, but it must not grant permissions.

**Selected pattern:** policy resolver producing a typed authority snapshot.

**Rejected:**

- using `AgentChatContextScope.WorkspaceScope` as final authority,
- parsing project IDs from prompt or workflow payload,
- letting tool middleware infer access from view text.

**Proof:**

- forged observation scope cannot expand access,
- a project switch revalidates access,
- read-only agent cannot mutate even when UI observation says mutation is available.

## PS-04 — Contributor strategy for active UI views

**Force:** Canvas, Gantt, Calendar, and Manager Summary own different facts.

**Selected pattern:** one atomic module publication composed from view-specific contributors.

**Rejected:**

- one ever-growing builder switch,
- independent non-atomic publishers racing for the same active scope,
- reading private Razor fields from the runtime.

**Proof:**

- each view contributor has direct unit tests,
- publication remains atomic,
- Gantt contributes bounded projection facts,
- inactive view facts are absent.

## PS-05 — Abstract factory for scope-bound workspace services

**Force:** file, command, artifact, path, process, MCP, and receipt services must share one scope.

**Selected pattern:** `IWorkspaceRuntimeServicesFactory` returning an owned cohesive bundle.

**Rejected:**

- resolving whichever scoped service happens to be present,
- constructing fallbacks inside MAF,
- mixing organization-bound services with project-bound plugins.

**Proof:**

- every service exposes the same scope identity,
- mismatched bundles fail before tool attachment,
- tests cover organization and project scopes.

## PS-06 — Narrow runtime ports with transitional facade

**Force:** execution, continuation, diagnostics, and administration change independently.

**Selected pattern:** interface segregation plus a temporary delegating facade.

**Rejected:**

- keeping one broad interface and merely moving methods to partial files,
- a generic `Execute(string operation, object request)`,
- multiple implementations that all forward to a god runtime forever.

**Proof:**

- Core execution depends only on execution/continuation ports,
- provider settings UI depends only on diagnostics/administration,
- source assertion blocks new `IAgentRuntime` callers,
- facade is removed in SB18 after SB17 stabilization.

## PS-07 — Anti-corruption layer for MAF

**Force:** MAF SDK sessions, events, tool calls, and workflow events change independently from application contracts.

**Selected pattern:** adapter + version bridge + state envelope.

**Rejected:**

- storing raw SDK objects in domain models,
- reflection scattered through application code,
- application code parsing MAF session JSON.

**Proof:**

- adapter contract is SDK-free,
- MAF-specific reflection is isolated,
- incompatible state envelope fails explicitly.

## PS-08 — Recovery policy chain outside MAF

**Force:** provider/finalizer failures may require domain-specific recovery.

**Selected pattern:** generic runtime evidence plus registered outcome recovery policies.

**Rejected:**

- process artifact recovery in MAF,
- generic Core guessing a process artifact path,
- returning success from prose without current-run proof.

**Proof:**

- Processes recovery tests instantiate the process policy directly,
- MAF contains no process type or source-kind branch,
- recovered output goes through the normal completion validation path.

## PS-09 — Provider-backed lightweight LLM invocation port

**Force:** ordinary workflow LLM nodes and a future ordinary chat need text/JSON inference without agent tools, memory, handoffs, approvals, finalizers, UI context, or MAF agent sessions.

**Selected pattern:** SDK-free stateless `ILlmInvocationPort` plus a provider-backed adapter over the existing provider runtime pool and `IProviderChatCompletionDriver`. Add a streaming sibling only when provider evidence supports a coherent contract.

**Why:** this preserves one provider credential, dispatch, lifecycle, retry, protocol, and usage architecture while giving application callers a small model-inference seam. It keeps ordinary inference below agent execution without coupling workflow/application code directly to provider drivers.

**Rejected:**

- constructing a temporary agent and session;
- a reduced or tool-disabled `IAgentRuntime`;
- workflow code calling concrete provider drivers/runtime handles;
- parsing project scope or authority from payload;
- a second HTTP/credential/retry/usage implementation in the LLM layer;
- an opaque `Dictionary<string, object>` options bag.

**Proof:**

- workflow LLM tests do not instantiate the agent runtime;
- direct-port tests use a fake provider runtime/driver;
- no tools, context contributors, memory, finalizers, approvals, handoffs, or workspace services are attached;
- payload `projectId` or path values cannot change authority;
- provider/profile/model, response format, usage, cancellation, and sanitized failures preserve characterized behavior;
- the selected provider driver is invoked exactly once;
- dependency guards keep abstractions SDK-free and agent-free.

## PS-10 — Ordinary LLM conversation above the stateless port

**Force:** the product will later need a simple multi-turn LLM chat that has transcript/history behavior but is not an agent.

**Selected pattern:** a separate application-level `ILlmConversationService` and store own conversation identity, transcript persistence, model/provider choice, summarization/compaction policy, and usage aggregation; each inference delegates to the stateless LLM port.

**Rejected:**

- representing ordinary chat as an `AgentDefinition` with disabled capabilities;
- reusing MAF agent session serialization as the canonical transcript;
- placing transcript persistence inside provider drivers or the stateless invocation port;
- automatically injecting product/UI context into ordinary chat.

**Proof in this bundle:**

- contracts and architecture tests demonstrate the dependency direction;
- the stateless port remains unaware of conversation persistence;
- no ordinary-chat UI or persistence migration is added prematurely;
- future work has explicit owners for transcript, compaction, provider state, and optional context consent.
