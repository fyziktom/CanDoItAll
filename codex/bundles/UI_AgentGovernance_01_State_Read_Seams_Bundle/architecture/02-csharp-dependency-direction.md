# Dependency direction and extraction gate

The [post-Overview evaluated graph](../proof/preparation/post-overview-evaluated-graph.json) contains 14 live projects and no project cycle or forbidden edge. It is inherited current-byte preparation evidence, not a freshly executed Governance graph.

UI currently uses existing Models, Usage, Conversations.Components and live BaseLib/Charts; Conversations uses BaseLib/OverlayLib. Models points to its existing lightweight contracts. Sandbox references UI. The module and broad AgentFramework.Components already reference UI. Live Components/FileTools mode must remain unchanged. Source csproj package declarations alone do not describe evaluated live references.

Target: Module -> UI; broad AgentFramework.Components -> UI for AgentRuntimeDetailsDialog's moved timeline/metrics children; Sandbox -> UI. No new project is needed and no additional dependency is expected: existing Models carries all required display value types. Read/session contracts remain Module. Runtime persistence and provider-native detail enrichment stay Core behind the workspace.

Forbidden UI/sandbox dependency closure: Modules.AgentFramework, AgentFramework.Core, Persistence, ProviderManagement runtime, Voice, AppComponents, broad AgentFramework.Components, query implementations or production composition. No reverse edge can be added to keep a moved child compiling. Update direct consumers/imports instead. Preserve existing package/source switch and disabled repository package publication; do not infer external binary compatibility from source builds. Recheck any actual public package policy before moving the two existing public components.

G00 and G03 evaluate actual MSBuild ProjectReference closures with current SDK/mode and retain path/edge/cycle output. G03 reruns after moves and compares source/package/static assets. Scoped CodeAnalytics is supplementary and must load nonzero relevant types; preserve diagnostics and do not treat an empty dependency filter as no edges.

Direct builds at the point of change: Module and its test owners in G01/G02; UI, broad AgentFramework.Components, Module, Web, sandbox Parity/Fast and owning Unit/Components/Integration in G03. Models/Core only if actually modified after a separately justified correction. Shared public assembly movement is a named reason to broaden consumer validation, not permission to change unrelated modules. Exact build/test inventory is frozen after source discovery, before execution.
