# Target Solution

## Inventory Precision

- Keep the host MCP thin and implement inventory classification in the sibling application-service layer where snapshot facts are already assembled.
- Preserve current factual coverage while separating product references from supporting references in the response model.
- Prefer additive response changes over destructive API removal so older clients keep working.

## Focused Context Compatibility

- Handle the legacy `Behavior` intent as a compatibility alias for `TroublePath`.
- Keep the existing `TroublePath` traversal semantics unchanged.
- Keep alias handling explicit and narrow so invalid future aliases still fail deterministically.

## Integration Boundary

- Sibling repo owns analysis logic and response shaping.
- Host MCP owns request/input normalization, tool contracts, and reinstallable server packaging.
- Repo skill owns query-path guidance for Codex and should be updated only if the shipped tool semantics change materially.
