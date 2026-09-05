# Current coupling and resulting risks

AgentsHomePage coordinates workspace/usage/chat/catalog services, notifications/dialogs, route compatibility, selection context, and direct EF bound-resource counts. Moving this into one unconditional overview fetch would change lazy Providers/RequestHistory behavior. Moving every catalog effect into the page would increase its existing responsibilities.

AgentCatalogPanel mixes catalog loading/repair, selection, context readiness, requested-agent opening, team operations, chat launching, and details dialog presentation. Selection is not equivalent to opening an editor. The page currently uses SkipCatalogRepair and initial data; duplicate repair/load behavior must be characterized before moving ownership.

AgentDetailsDialog holds a mutable AgentEditorModel and edit context, loads references with different error policies, normalizes and saves, deletes, resets, and handles capability writes. It crosses Projects/Security/Workspace/infrastructure boundaries. Its real subtree also reaches storage, external roots, shared provider refresh, avatar generation, memory profiles/drivers, and capability setup.

A same-project interface removes direct knowledge from a component but does not remove the module's runtime/persistence project references. Public use of ProjectAccessListItem and SecretListItem also retains implementation-assembly edges. Source-level parent injection checks miss both problems.

The revision therefore changes ownership first, proves preserved behavior immediately, and inventories the complete candidate graph before any later physical extraction.
