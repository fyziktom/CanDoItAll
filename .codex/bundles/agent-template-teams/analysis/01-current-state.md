# Current State

- Default serious-delivery agents were previously declared in `SandboxWorkspaceSeedBuilder` as C# literals with provider, access, capabilities, and instructions loaded from `SeedAssets/instructions/agents`.
- The seed normalizer only refreshed a hardcoded set of managed template keys and normalized existing teams instead of merging newly seeded default teams.
- `Templates` already exists for processes and workflows, making it the natural home for editable default agent templates.
- Existing integration tests covered catalog normalization and seed expectations, but needed an explicit check for file-backed default agent teams.
- UI validation is required because the change affects the seeded default agent catalog that users browse/select in the app.
