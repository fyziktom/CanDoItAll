# Current state

Baseline d3ba280a431bfe74ce03a72638ac06dff47de660; CodeAnalytics snap-20260905133125-38ffcf5c. Exact type search and member inventory confirmed AgentProviderProfilesPanel.razor.cs, 474 lines, three injected services (runtime administration, provider administration, notifications), no explicit constructor, direct EditContext/new draft construction.

Owned together: two catalog reads, editor lookup, implicit draft-Id selection, numeric tab, secret list, source-managed policy, tree/search, model/tag text, all mutations and overlay. No cancellation/disposal boundary. Six lazy sections render pricing/thinking/sharing/history children; SharedProviderSourcesDialog is conditional. Child internals remain outside this slice.

Inspected contracts: ProviderManagement/Contracts/ProviderRuntimeAdministration.cs and ProviderAdministration.cs; module UI registration; panel Razor/codebehind; four existing component test classes (ProviderAdministrationLayoutTests, ProviderCatalogRefreshTests, AgentProviderProfilesPanelPricingTests, SecretProviderSelectionTests).

Missing proof: pending A/B reads, failed target/retry, reset/disposal while reads ignore cancellation, explicit secret partial failure, authoritative selection. Existing tests cover pricing preservation, discovery/save, tags/kind defaults, lazy history/overlay, separate history form, EditContext retention and saved secrets.
