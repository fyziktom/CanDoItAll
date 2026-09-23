# Agents and Storage integration

This adapter publishes the existing Storage catalog, browse, read, write, and delete
tools through `IAgentRuntimeToolProvider`. Storage retains ownership of its catalog,
driver capabilities, locators, and byte operations. Core and MAF consume the generic
configured-workspace provider contract without implementing Storage behavior.

The adapter attaches in the configured-workspace phase. It follows
`WorkspaceToolsEnabled` independently of `RuntimeToolProvidersEnabled`, preserving
the original grants, enabled and read-only catalog checks, capability rules,
bounded reads, approval requirements, tool names, and attachment order. Runtime
composition rejects an unplanned function or a denied configured identity.

`StorageToolPolicy` owns the five tool metadata records. The published runtime
descriptor and policy data are runtime contracts; the existing serialized Storage
grant fields remain compatible. This adapter preserves the current effect behavior
and adds no receipt or automatic mutation retry guarantee.

Validation covers the actual MAF provider composer and Process preflight, all four
workspace/provider flag combinations, metadata and schema fingerprints, approvals,
catalog and driver restrictions, bounded reads, and invalid locator rejection.
