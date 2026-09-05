# Current ownership and provenance
UI: AgentProviderProfilesPanel (445-line codebehind plus six sections), ProviderProfilesSession/State/Reads, ProviderProfileEditorForm, pricing/thinking editors. SharedProviderManagementPanel and SourcesDialog combine effects, mutable state and rendering. None uses a target-owned write session.

Backend: ProviderManagement owns canonical profile registry, secret scope, source/reconciliation, publication/import state and guards. Core registry interface and Models editor are consumed beyond UI. AgentFramework module composes runtime materialization and post-commit observers; Web AgentsApi publishes administration endpoints. Models/Core must not reference product/UI.

CodeAnalytics before: UI snap-20260905161443-38ffcf5c (194 docs); backend snap-20260905162158-7692d56b (79 docs). Scoped project snapshots omit some referenced-source edges; project files and runtime composition remain authoritative. Inspect after snapshots at closure. Components MCP library and recommendations both returned Transport closed; use exact readonly sibling Alert/Button/Dialog contracts before changing composition.

Clean live siblings: Components c3e6aa03a878994c0ba8aed6af017d0be75f3796 (CI pin), FileTools 7c7453c6583365ae5bd63f8fc6efc4a776e15818 (CI pin differs: 498b36825bd5a5222429972af120b04becf4b3f6), SharedInfo 00e6fe6389eba9a6bc3b2bb78e27fb10b7597292. Preserve current source checkouts. SDK global.json 10.0.302 with latestPatch; record effective SDK in executed proof.

Providers-01 hashes/proof are historical baseline; do not rebrand the prior 9462 cases as this run. Known repository docs finding is 118 tracked old logs; reverify final delta and do not suppress it.
