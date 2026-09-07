# Current dependency direction

O03's evaluated live graph contains 14 projects and no cycle or prohibited runtime edge. Before the move the same sandbox root had 12. The two additions are the existing Usage value-contract library and actual Charts library. The UI uses Usage values, not its query execution. The sandbox registers chart/UI services only; all page/session/query/HR/defaults/navigation/dialog effects remain in Module.

| Project | Evaluated direct project dependencies |
|---|---|
| CanDoItAll.AgentFramework.UiSandbox | CanDoItAll.AgentFramework.UI |
| CanDoItAll.AgentFramework.UI | CanDoItAll.AgentFramework.Usage, CanDoItAll.AgentFramework.Models, CanDoItAll.Conversations.Components, CanDoItAll.Components.BaseLib, CanDoItAll.Components.Charts |
| CanDoItAll.AgentFramework.Usage | CanDoItAll.AgentFramework.Models |
| CanDoItAll.AgentFramework.Models | CanDoItAll.AgentFramework.Capabilities.Abstractions, CanDoItAll.Memory.Abstractions, CanDoItAll.SharedKernel, CanDoItAll.Infrastructure.Abstractions, CanDoItAll.AgentFramework.ProviderHistory.Abstractions |
| CanDoItAll.Conversations.Components | CanDoItAll.Components.BaseLib, CanDoItAll.Components.OverlayLib |
| CanDoItAll.Components.BaseLib | CanDoItAll.Components.Common |
| CanDoItAll.Components.Charts | None |
| CanDoItAll.AgentFramework.Capabilities.Abstractions | None |
| CanDoItAll.Memory.Abstractions | None |
| CanDoItAll.SharedKernel | None |
| CanDoItAll.Infrastructure.Abstractions | None |
| CanDoItAll.AgentFramework.ProviderHistory.Abstractions | None |
| CanDoItAll.Components.OverlayLib | CanDoItAll.Components.BaseLib |
| CanDoItAll.Components.Common | None |

The receipts use evaluated MSBuild references, not the static project's package/project text. Live sibling mode is preserved. Scoped UI CodeAnalytics complements this graph and reports no cycles/services; it does not erase pre-existing Module namespace/type cycle findings or establish whole-solution acyclicity.

Forbidden UI/sandbox owners remain Module, Core, Persistence, provider runtime, Voice, AppComponents and broad AgentFramework.Components. No old wrapper points back into Module. AgentUsageDisplay has one honest public pure owner because retained Module consumers use it directly. The small existing Usage contracts are reused without moving application snapshots or inventing duplicate DTOs.

The O02 BaseLib source exception remains required: a bounded opt-in same-page navigation lease preserves unrelated dialogs while the page cancels its own references. This is an additive public dependency, separately tested and included in the broad stable invalidation. Its live sibling source/approval bytes must accompany the app change. FileTools is unchanged. No package-mode substitution or further sibling change is part of O03.

The original preparation graphs remain in inventory. Current receipts are retained under proof/O03/raw and proof/O03/final. Any later graph or asset drift invalidates the dependent extraction/browser/measurement proof and requires fresh evaluation.
