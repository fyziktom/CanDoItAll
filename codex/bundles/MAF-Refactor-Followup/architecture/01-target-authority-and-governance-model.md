# Target authority and governance model

## Canonical concepts

| Concept | Owner | Persistence | Authority |
|---|---|---|---|
| UI observation | active module/UI registry | transient/versioned | descriptive only |
| Conversation affinity | AgentFramework application | scoped/durable as chosen | decides follow/detach, not permissions |
| Turn context reference | AgentFramework Core | execution metadata | identifies captured observation |
| Execution authority | source authority provider | safe projection + in-memory lease | canonical scope/read/mutation grant |
| Execution governance snapshot | application/Core composition | run lease + safe fingerprint | single runtime policy input |
| Capability access plan | runtime application | per-run | monotonic projection of governance |
| Tool proposal | application governance | durable | operation requesting approval |
| Approval decision | application governance | durable | authorizes/rejects one proposal |
| MAF session state | MAF adapter | opaque versioned envelope | continuation mechanism only |

## Required invariants

1. The UI can report a source/scope but cannot grant it.
2. The authority provider independently validates source identity, profile, object existence, and agent access.
3. Capability composition cannot include a tool whose operation class is outside the governance snapshot.
4. Invocation policy cannot widen the composition plan.
5. A tool provider may validate domain invariants and further narrow access; it must not independently grant broader access.
6. Approval is proposal-specific and cannot change the governance snapshot for unrelated future calls.
7. Continuation binds to the original governance fingerprint and workspace identity.

## Suggested contracts

```csharp
public sealed record AgentExecutionGovernanceSnapshot(
    AgentExecutionAuthorityId AuthorityId,
    Guid AgentId,
    Guid DatabaseProfileId,
    DatabaseProfileGeneration DatabaseProfileGeneration,
    WorkspaceExecutionScope Workspace,
    bool ReadAllowed,
    bool MutationAllowed,
    IReadOnlySet<string> AllowedOperations,
    IReadOnlySet<string> AllowedCapabilityKeys,
    IReadOnlySet<string> WritableExternalTargetAliases,
    IReadOnlySet<string> ReadOnlyExternalTargetAliases,
    IReadOnlySet<string> AllowedManagedArtifactReadRefs,
    string PolicyVersion,
    string PolicyFingerprint);
```

The exact shape may differ, but it must be SDK-free, immutable, and provider-neutral.
