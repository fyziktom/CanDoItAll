# SB02 Semantic Invariants

- Invariant ID: `SB02-manager-resolution`
- Source raw note: N002 manager tab cannot connect selected run manager.
- Expected behavior: Opening Manager chat for a selected process run resolves the configured manager when present, otherwise resolves a unique assigned manager before using non-ambiguous fallback options.
- Disallowed shallow implementation: Do not suppress the unresolved-manager error without a real technical agent id; do not choose among ambiguous manager candidates.
- Failing-first test: `ProcessManagerAgentResolver_uses_assigned_manager_before_ambiguous_manager_options` fails when the UI only checks fallback manager options.
- Passing test: `ProcessManagerAgentResolver_rejects_ambiguous_assigned_managers` passes only when tied assigned managers are rejected.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerAgentResolver.cs`, `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerChatService.cs`, `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.ManagerChat.cs`.
- Production assertions: Browser proof shows selected completed run label, resolved manager, and ready composer.
- Red-team negative case: Equal-scored assigned manager candidates resolve to null, preserving explicit ambiguity instead of hiding it.
- Downstream dependency check: The shared resolver keeps Processes page behavior aligned with manager chat service behavior.
