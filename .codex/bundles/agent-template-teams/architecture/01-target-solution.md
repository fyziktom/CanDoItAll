# Target Solution

- `Templates/Agents/manifest.json` declares the template pack and the default team folders.
- Each `Templates/Agents/teams/<team-key>/team.json` owns team-level metadata/settings and references member folders.
- Each `Templates/Agents/teams/<team-key>/members/<agent-key>/` owns `instructions.md`, `settings.json`, and `skills.json`.
- `AgentTemplatePackLoader` resolves the repository `Templates/Agents` directory, parses the pack with structured JSON APIs, and exposes typed template records to the persistence seed builder.
- `SandboxWorkspaceSeedBuilder` materializes `AgentDefinition` and `AgentTeam` instances from the template pack, resolving provider keys and capability keys against existing seed catalogs.
- `SandboxWorkspaceSeedNormalizer` merges seeded teams and refreshes seeded template-backed agents without relying on a hardcoded managed-template-key list.
- Embedded agent instruction seed assets are removed; remaining `SeedAssets` text is limited to non-agent skills/resources.
- Tests and browser proof close the risk that the new template path only exists on disk without producing the same app-visible default agent catalog.
