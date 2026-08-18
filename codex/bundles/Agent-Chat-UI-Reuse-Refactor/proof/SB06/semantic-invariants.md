# Semantic invariants — SB06

- Agent labels remain Name, Role title, Tags, Summary, and Instructions; future consumers can supply different labels without importing Agent types.
- Existing Agent identity and runtime `data-testid` values remain present.
- Avatar selection, clear/default, generation, and persistence remain Agent-owned.
- `ProviderProfile` and provider `Guid` values never cross the neutral boundary; the Agent facade performs explicit opaque-key mapping and rejects invalid keys.
- Provider default, suggested model, and custom override normalization retain the existing public Agent facade behavior.
- Agent reasoning effort and runtime temperature-omission policy remain unchanged and outside the neutral component.
- Save, delete, version conflict, validation, service calls, and dialog close behavior remain in `AgentDetailsDialog`.
- Identity, Runtime, Memory, Images, Project Structure Access, Workspace Tools, Secrets, Process Access, Capabilities, and Voice tabs remain present and in the same order.
- The optional temperature field is reusable presentation only and is not added to the current Agent workflow.
- No Simple Chat route, catalog, filter, context, API, SSE, or settings surface was introduced.
