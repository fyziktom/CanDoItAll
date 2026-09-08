# Diagnostics execution

Entry: components-decoupling, e9fb82be7a43f843303cbd81233450acb6d24dd0 (local and remote). No commit, staging or history operation is authorized. Components remains c3e6aa03a878994c0ba8aed6af017d0be75f3796 plus its existing five-file patch; remote main bd1eb1030c438c861b94b3b8d3b9dba72925d685. FileTools remains clean at 7c7453c6583365ae5bd63f8fc6efc4a776e15818; remote main 3a080ecd31068a77c1e1bd639f7a78e21c93db85 (verified through HTTPS because its SSH remote is unavailable).

## Implementation and architecture gate

AgentDiagnosticsReads delegates to the canonical workspace. AgentDiagnosticsSession owns three independently accepted read lanes, request identities, cancellation and stale-result fencing. The host owns rendering notifications and effect publication; its intent revision suppresses notifications from superseded refreshes and disposal. Sessions execute on the owning Blazor dispatcher. Token sources are canceled when superseded or disposed, but the completing read disposes them. Lane retries dispatch only that lane. Agent labels enrich valid rows without invalidating the run window.

Concurrent reads are supported by the production workspace store's semaphore/cross-process read coordination and the existing dashboard's own concurrent reads; the registered-adapter integration test executes the three actual production reads together. They are not represented as one atomic snapshot. Dashboard failures describe the global count, while failure rows derive only from twelve accepted recent runs.

The pure UI closure has immutable presentation, fixed public errors and typed Refresh/Retry intents. It has no injected services or new project dependencies. Existing bounded Unicode/invariant-UTC formatting policy is reused without modifying Governance. All production boundary producers were inspected: LocalWorkspaceProcessHost creates PolicyOnlyLocal; the unconfigured descriptor is Unknown; command runners only delegate. These modes receive explicit reviewed display descriptions. Unrecognized modes remain unverified. Arbitrary host labels, scopes, notes, paths, URLs, environment/credential material, input/result content, metadata and serialized runtime state are omitted. Logs record lane, revision and exception type; raw messages are deliberately excluded because they can contain private payloads.

The sandbox has eight controlled scenarios and a real domain poison fixture passed through the production mapping. Boundary fields use existing FormField/Grid components so the recent runs remain visible at the desktop viewport. The page is the scroll owner. No timeline/metric implementation was copied. Components MCP returned Transport closed; the existing sibling components and their source contracts supplied composition guidance. Entry CodeAnalytics snapshot: snap-20260908134543-dbf24abe. Evaluated UI/sandbox closure: fourteen projects, no forbidden dependency.

## Focused progression gate

15 unit, 11 component and 1 registered-adapter integration cases pass with zero skips. The component inventory includes actual asynchronous host publication/removal, not only service-free rendering. One test-harness correction uses the repository bUnit component-disposal helper; disposing a render handle does not dispose the component. No accepted product assertion was weakened.

Web browser checks exercise automatic load, all three partial/stale failures and lane retries, poison mapping and normal refresh. Parity and Fast each cover all eight specimen states, encoding/accessibility text, overflow and controlled refresh. The Web bootstrap explicitly accepts the startup database profile before entering Diagnostics through the real tab. Earlier harness attempts interacted with prerendered HTML during startup and are not passing evidence.

Diagnostics focused/extraction/browser progression permits the authorized History slice. Final predecessor regression, static gates, broad stable execution and watcher smoke remain pending until both source surfaces freeze. Historical Governance proof is unchanged.
