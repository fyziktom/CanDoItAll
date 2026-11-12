# Workspace provider comparison labels

Source-only follow-up to the parent's successful three-case browser lane: the current/previous comparison displayed the provider GUID instead of its available display name. No build, test or browser run was performed for this follow-up.

`WorkspaceDefaultsComparison` now takes the existing `WorkspaceProviderOption` list from its only two product callsites in `WorkspaceDefaultsSurface`. It resolves the exact saved ID for display only: supplied name, supplied name plus disabled status, None, or Unavailable provider. Catalog names describe the supplied reference list; they do not claim a historical name captured with the saved settings. The unavailable dropdown option also has a human-readable label while retaining the exact GUID in its value.

The draft, expectation, immutable save command and current/previous snapshot still retain their original IDs. There is no extra read, write, lookup service, authority decision or CSS. Existing FormSection/Stack/TextBlock rendering remains; only the two relevant rendering-register rows were refreshed. Components MCP tooling is unavailable in this session; this edit reuses the inspected existing components and introduces no composition or package.

One new `WorkspaceDraftTests.Provider_comparisons_show_catalog_labels_and_preserve_unavailable_reference_values` Fact covers all four display states, verifies no visible identifier leaks through the comparison, preserves the unavailable select option's exact ID and proves rendering does not call the scenario owner. The containing class now has 10 Facts plus 10 inline rows, **20 expected cases**; the focused new filter is **1 expected case**. These are source counts, not discovery or execution results.

Parent validation should refresh the UI build, run the focused Fact and inspect the existing large-desktop conflict/reconciliation specimen if a fresh visual proof is required. Prior browser evidence remains valid for the unchanged owner/lifetime path but predates this label correction.
