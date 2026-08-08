# Canonical context model

## Decision summary

The system needs multiple sources of truth because the concerns are different. The architecture must enforce one authoritative owner for each concern instead of storing the same meaning in several formats.

## 1. Canonical product state

Examples:

- Project Structure nodes and relations
- task schedules
- project actors/resources
- process runs and steps
- managed artifacts
- provider profiles

Owner: the product/domain module and its canonical persistence.

Rules:

- Gantt is a projection over canonical project/task data.
- AI output is a proposal or execution result until the owning module accepts it.
- UI fragments and MAF session payloads never become product truth.
- An agent mutation uses the owning module's typed command/tool boundary.

## 2. Live UI observation

Proposed name: `AgentUiObservationSnapshot`.

Purpose: describe what the user is currently looking at.

Typical fields:

```text
ObservationId
ScopeId
SourceKind
SourceId
WorkspacePosition
Surface
View
PrimarySelection
SelectedEntities
VisibleFacts
PublicationVersion
NavigationIdentity
CapturedAtUtc
FreshUntilUtc
ContributorReferences
OpaqueAttachmentLeases
```

Owner: the scoped UI observation registry.

Properties:

- ephemeral,
- current,
- immutable once captured,
- bounded,
- versioned,
- route-fenced,
- untrusted as model content,
- not an authorization object.

A UI observation may contain an expected project identity, but that identity is only a request to the authority resolver.

## 3. Conversation context affinity

Proposed name: `AgentConversationContextBinding`.

Purpose: represent what a floating chat thread is following.

Suggested shape:

```text
ConversationId or ChatSessionId
Mode
ContextEpochId
SourceKind
SourceId
DisplayName
LastSurface
LastView
Revision
AdoptedAtUtc
UpdatedAtUtc
LastTurnContextDigest
```

Initial modes:

- `FollowCurrentSurface` — default for floating agents.
- `Detached` — no application context is supplied.

Do not add `PinnedToSource` until a canonical rehydrator exists for inactive sources. A pin must never reuse stale opaque UI attachments.

Owner: conversation context service/store.

Persistence:

- before a chat session exists, hold a pending binding by floating-chat handle;
- once a session exists, persist the binding through an `IAgentConversationContextStore`;
- do not put it in MAF compatibility state.

## 4. Context transition

Proposed name: `AgentContextTransition`.

Purpose: explain the relationship between the conversation's previous binding and the newly captured observation.

Suggested transition kinds:

```text
None
ViewChanged
SelectionChanged
SourceEntityChanged
SourceKindChanged
ContextDetached
ContextUnavailable
```

Suggested decisions:

```text
Kept
AutoAdopted
Detached
Rejected
```

Rules:

- Canvas -> Gantt in the same project is a soft `ViewChanged`.
- Project X -> Project Y is `SourceEntityChanged`.
- Project Structure -> unrelated module is `SourceKindChanged`.
- A transition is application-generated trusted metadata, not user/UI prose.
- The transition is supplied on the next explicit turn.
- Do not append synthetic user messages merely because a tab changed.

## 5. Immutable turn context

Proposed name: `AgentTurnContextCapture`.

Purpose: bind one execution turn to the exact observation, transition, conversation binding, and authority that existed at admission.

Suggested runtime shape:

```text
TurnContextReference
ModelContext
ConversationBinding
Transition
ExecutionAuthority
CapturedAtUtc
Digest
```

Persist only `TurnContextReference` and safe authority identity/fingerprint. Keep opaque attachments request-scoped unless an attachment declares a separate rehydration policy.

Required invariants:

- one turn has one digest,
- the digest cannot change,
- approval continuation uses the same turn context,
- a current UI switch cannot retarget an admitted run,
- a new user turn captures a new context,
- an unavailable required lease fails closed.

## 6. Execution authority

Proposed name: `AgentExecutionAuthoritySnapshot`.

Purpose: state what the admitted turn may do.

Suggested shape:

```text
AuthorityId
AgentId
PrincipalId or local authority identity
DatabaseProfileId
DatabaseProfileGeneration
WorkspaceExecutionScope
ReadAllowed
MutationAllowed
AllowedOperations
AllowedCapabilityKeys
AllowedExternalTargetAliases
ReadOnlyExternalTargetAliases
PolicyVersion
PolicyFingerprint
ResolvedAtUtc
```

Owner: application authority resolver backed by canonical authorization services.

Rules:

- UI visibility/access entries are hints for filtering and early denial.
- The authority resolver independently revalidates the agent and source.
- Model text cannot broaden authority.
- A payload `projectId` cannot select authority.
- A view switch may change observation without changing authority.
- A project switch always creates a new authority snapshot.
- The workspace service bundle must be created from this resolved scope.

## 7. Durable execution state

Owner: execution run store.

Canonical facts include:

- admission identity,
- run state and outcome,
- pending proposals/approvals,
- receipts,
- artifacts,
- provider identity,
- usage,
- context reference,
- authority fingerprint,
- runtime state envelope reference.

Do not store full opaque UI attachments as execution truth.

## 8. Runtime adapter state

Proposed name: `RuntimeStateEnvelope`.

Purpose: contain provider/framework continuation state.

Suggested shape:

```text
AdapterId
SchemaVersion
AdapterPackageVersion
ProviderProfileId
ProviderTransport
Model
ToolsetFingerprint
ContextPolicyFingerprint
CreatedAtUtc
PayloadJson
```

Owner: adapter-specific serializer/compatibility policy.

Rules:

- MAF state is opaque to application/domain code.
- A mismatched envelope is migrated explicitly or rejected.
- The presence of MAF state does not override the execution run or conversation binding.
- Approval identifiers remain application-owned and are mapped to MAF request identifiers.

## Practical timeline

### Turn 1: Project X Canvas

1. UI registry publishes Project X / Canvas observation `v41`.
2. Floating chat binding follows Project X.
3. User sends a message.
4. Turn capture stores observation `v41`, no transition, authority `Project X`.
5. The run starts.

### User switches to Gantt during the run

1. UI registry publishes Project X / Gantt observation `v42`.
2. The active run remains bound to `v41`.
3. The chat UI may display: `Current run: Canvas; next turn: Gantt`.
4. No model call is made.

### Turn 2: same thread, Gantt

1. User sends the next message.
2. Turn capture reads `v42`.
3. Transition classifier emits `ViewChanged: Canvas -> Gantt`.
4. Conversation binding revision advances.
5. Authority remains Project X after revalidation.
6. The model receives current Gantt facts plus a trusted transition note.

### User switches to Project Y

1. New observation source ID is Project Y.
2. Transition classifier emits `SourceEntityChanged`.
3. The conversation starts a new context epoch; prior UI facts remain historical context only.
4. Follow mode adopts Project Y for the new turn.
5. Authority resolver validates access and creates a Project Y scope.
6. Earlier transcript remains conversation history, but the trusted context header states that the current source is Project Y and the prior epoch is historical.
7. An old Project X approval continues with the original Project X turn context only.

## Anti-model

Do not create a single record called `Context` containing:

- UI text,
- project scope,
- permissions,
- provider session,
- process IDs,
- tool configuration,
- conversation state.

That recreates the ambiguity this bundle is intended to remove.
