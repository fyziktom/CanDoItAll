# Entry adjudication before implementation

Entry source and evidence: [inventory](../inventory/entry.json). All 22 requested findings were independently checked against current source, tests, Git tree and surviving originals.

| Finding | Decision | Evidence and disposition |
|---|---|---|
| 1 | Confirmed | SourceVerification Synchronize uses SetEquals over selected imports, with source token/time/status. Preserve. |
| 2 | Confirmed | TargetVerification captures typed exact publication/settings/retirement intent and identities. Preserve. |
| 3 | Confirmed | ChangeDelivery exposes ReconcileAsync and IsAcknowledged. |
| 4 | Confirmed | Recovery.DeliverAsync calls internal Acknowledge after any successful current callback. |
| 5 | Confirmed defect | No-op callback is consequently consumed as acknowledged. |
| 6 | Confirmed defect | SharedDeliveryLifecycleTests.Successful_acknowledgement_prevents_second_delivery_and_releases_attempt explicitly asserts no-op acknowledgement; related fixtures repeat it. |
| 7 | Confirmed with audit | Provider panel wraps complete ReconcileSharedAsync and throws on incomplete result; AgentDetails wraps lifetime-checked provider reread; ThinkingEditor forwards the envelope unchanged. Contract enforcement is missing. |
| 8 | Confirmed defect | The Boolean check before await admits concurrent receiver delegates. |
| 9 | Confirmed | .gitignore line 20 ignores proof/ anywhere. |
| 10 | Confirmed | Both current local/remote trees have zero proof entries; local original evidence exists. |
| 11 | Confirmed | Predecessor manifests have 48 and 135 entries, all existing and hash-matching on disk, but proof is not in Git. |
| 12 | Confirmed | Tracked source/closure prose is reviewable; ignored originals are not branch evidence. Repair in Harden02, without inventing timings. |
| 13 | Confirmed | Compiled Parity/Fast mode, separate outputs and sandbox-local generated theme exist; exact hashes retained in inventory. |
| 14 | Confirmed | Frozen direct-watch.cjs owns local dotnet watch, Tailwind and browser with monotonic clock. |
| 15 | Confirmed historical claim | Original direct-results and run ledgers exist and match checksums: 27 primary successes, 18 reloads, nine static updates; historical failure exclusions remain separate. |
| 16 | Confirmed | Catalog.razor stores scenario, matchedLayout and selection only in local fields. |
| 17 | Confirmed | CapabilitiesPanel source owns all listed reads, editor, effects, filters, tree and access callbacks. |
| 18 | Confirmed | All five listed injected services are present. |
| 19 | Confirmed | AgentsHomePage supplies effectiveRequestedAgentId and receives selection/access callbacks. |
| 20 | Confirmed | AgentCapabilityList public parameter/intent contract has no service injection, in broad Components assembly. |
| 21 | Confirmed | Components.csproj already references AgentFramework.UI; reverse reference would cycle. |
| 22 | Confirmed deferred defect | ToggleCapabilityAsync replaces editor.SelectedCapabilityIds before Save and catch does not restore. Characterize in Capabilities01; mutation hardening belongs to 02. |
