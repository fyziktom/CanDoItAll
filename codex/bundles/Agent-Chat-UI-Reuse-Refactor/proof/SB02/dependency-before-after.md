# SB02 dependency before/after

## Before

Snapshot `snap-20260816102508-c82f9e5f` contained four scoped product projects. `CanDoItAll.AgentFramework.Components` had no application-owned neutral conversation dependency. No scoped project cycle existed.

## After

Snapshot `snap-20260816110147-d3f1a4be` is healthy with 5 scoped projects, 329 documents, 770 types, and 7,493 members.

- `CanDoItAll.Conversations.Components` has no project references.
- Its only packages are `CanDoItAll.Components.BaseLib` and `Microsoft.AspNetCore.Components.Web`.
- `CanDoItAll.AgentFramework.Components` points to `CanDoItAll.Conversations.Components`.
- Existing module consumers continue to point at AgentFramework.Components/AppComponents; production rendering is not migrated.
- No project cycle exists.
- The two pre-existing intra-project module/type cycles from SB01 remain unchanged and do not involve the neutral project.

The source scan and executable repository boundary guard both pass. There is no neutral reference to AgentFramework, LlmChats, EF Core, persistence, runtime services, service location, provider SDKs, or backend entities.

