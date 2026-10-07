# AgentFramework rendering UI

## Request History

`History` owns the complete `ProviderHistoryWorkspace`: raw filter draft and validation,
immutable applied queries and cursor trail, results, metadata and separate read-only content
dialogs. Both production entry points render this workspace. `IProviderRequestHistory` is the
only read port; the UI neither constructs access contexts nor decides canonical-owner permission.
The production `ProviderRequestHistoryPanel` retains service resolution, authentication/profile
notifications and activation retirement. The authorized application service remains unchanged.

Every rendered result action captures a `HistoryViewOrigin`. Search, paging, cancellation,
clearing and detail activation invalidate earlier actions. Metadata/content callbacks also retain
their exact activation and owner reference. Disposal clears owned data and callbacks; request
tokens are disposed only after their reads unwind. Collapsing advanced filters keeps raw input
validation mounted. Opening or editing the workspace never reads history.

The [History boundary record](../../../docs/architecture/provider-history-ui-pp3.md) maps ownership
and proof. The existing sandbox renders these exact controls with layered synthetic reads.
Independent tests live in `tests/Components/CanDoItAll.AgentFramework.UI.Tests`; production context
wiring remains covered by `ProviderRequestHistoryPanelTests`.

## Catalog and capabilities

`Teams` owns the real technical-team metadata editor, Material icon picker and member selector.
Metadata submissions contain only identity/name/description/icon. Native dialogs provide the
coordinated metadata operation and keep persistence/profile authority in the module. Member
selection returns an immutable set tied to its opening team; AgentCatalogHost performs the
native update and separately reconciles catalog reads. Missing references remain visible until
explicitly removed, and private-provider badges confer no permissions.

The independent `/teams` sandbox includes new/existing/empty/missing-reference/large, rejected,
unknown, deleted, failed-load and held-operation fixtures. Its stored records are separate from
drafts and its two editors use the same real components. It registers no native effect services.

This Razor class library owns the controlled AgentCatalogPanel, its snapshot/selection/intent contracts, the real AgentSelectionCard and the pure participant presentation mapper. The card and mapper retain their existing namespace for consumer compatibility; their assembly is this UI project.

The capabilities boundary also owns the real AgentCapabilitiesSurface, AgentCapabilityList and immutable selection/load/access/intent/presentation contracts. AgentDetailsDialog and the standalone surface consume the same list. Application operation outcomes, recovery, sessions and Curator launch state remain in the module; the effect host maps them to presentation records.

AgentCatalogHost, AgentCapabilitiesPanel, dialogs, chat launch, persistence and provider/runtime effects remain in the AgentFramework module. The rendering boundary depends on Models, Usage value contracts, Conversations components and the existing BaseLib/Charts UI primitives. Repository source mode supplies live sibling components; this project does not reference the broad AgentFramework.Components assembly.

Build from the repository root:

    dotnet build src/UI/CanDoItAll.AgentFramework.UI/CanDoItAll.AgentFramework.UI.csproj --configuration Release

The [catalog sandbox](../../Sandboxes/CanDoItAll.AgentFramework.UiSandbox/README.md) exercises this same implementation with controlled snapshots. Preserve its real card/tree/tooltips, CSS isolation, fonts and generated theme assets when changing composition. Existing catalog component tests cover public rendering and intents; production host tests cover effects and lifetime.

## Overview boundary

The real AgentsOverviewSurface, immutable AgentsOverviewState/intents, pure presentation mapper/options, ProviderUsageConsumerList and AgentUsageDisplay live in `Overview`. The formatting helper is shared with the complete Usage detail family. The retained legacy AgentOverviewUsageList has no current product caller; see the [current census](../../../docs/architecture/agents-renderer-census.csv).

AgentsHomePage owns the page-lifetime session, independent Header/Overview/Usage reads, route state, HR/defaults/team effects and its three usage-dialog lifetimes. The UI library registers no application queries, usage projection sources, persistence or runtime effects. Existing application snapshot types remain in Models/Usage and are copied into immutable presentation at the effect boundary.

The existing sandbox has an Overview specimen with actual Charts registration/assets and controlled state. Model rows remain lazy detail content rather than dashboard content.

## Shell and Usage detail family

`Shell/AgentsShellSurface` renders the native page's header, independent readiness/counts,
help, exact tab items and typed commands, with a slot for native tab content. Every command
captures its rendered scope and tab. Route parsing, HR launch, default feeding and context
publication remain in AgentsHomePage. `AgentDefaultsConfirmation` returns only the explicit
decision; the native dialog closes its own reference.

`Usage` owns the actual consumer/provider/model metric cards, charts, paged grids and
Close/Retry frame. It consumes the accepted ProviderUsageQuery and ProviderUsageSnapshot
without re-resolving time or querying during paging. Cell templates capture their rendered
snapshot and share denominator, including while the original view retires. Duplicate
display labels never merge identity-bearing rows. Unknown/unpriced/partial evidence stays
distinct from empty data.

The public native dialog adapters compose UsageDetailHost. That host owns one bounded read,
query equality, frozen collections, cancellation and safe errors. A parent cancellation,
profile notification or changed authentication cascade retires the original view and clears
its data. Late reads cannot publish or retry against a replacement owner. Query equality is
not used as a profile or actor authority token. A failed read retains Close and an explicit
same-window Retry. No runtime service is registered in this UI library.

The independent `/completion` sandbox uses these same surfaces. The [AC1 record](../../../docs/architecture/agents-completion-ac1.md)
tracks native and browser proof separately from final native consumer delivery.

## Runtime and conversation adjuncts

`Runtime` owns the actual selected-run details, compatibility facts, timeline/metrics and
execution log with exact highlight/run identities and safe copy. Native adapters sanitize
and bound strings before creating immutable presentation. Runtime objects and redaction
policy never enter this library.

`Chat` also renders context/affinity, close choices, typed activity status, recovery/runtime
actions and the keyboard-accessible image input. Intents retain the rendered generation,
conversation, run or operation identity. The product still owns context bindings, stream
readers, durable approval decisions, cancellation and attachment staging. Stopping a floating
handle does not reject its durable pending approval.

`Avatars/AvatarPickerSurface` renders the complete picker and owns its form/request lifetime.
Three typed operations provide source lookup, image generation and validated upload. Native
hosts retain image policy, credentials and notifications. Closing/reopening or retiring the
editor cancels the old request; late callbacks cannot update another activation. Existing
public avatar value types retain their namespace and native assembly type forwarders.

The independent `/adjuncts` specimen renders these same components. Its file input reads
owned bytes and displays their hash; its avatar upload uses the shared Models image policy.
It does not stage native attachments, admit runs or call a paid image service. Native and
independent tests exercise separate ownership responsibilities.

The [renderer/caller census](../../../docs/architecture/agents-renderer-census.csv) names
all current destinations and retained native responsibilities. Its [asset closure](../../../docs/architecture/agents-renderer-assets.md)
traces shared fonts/theme, Charts, dialogs/copy, floating windows, avatars and native
voice/download modules. Published source equivalence and real native consumers are
recorded separately in AC1; a sandbox callback is never persistence or authorization proof.
