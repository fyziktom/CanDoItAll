# Normalized Requirements

- R001: Create `Templates/Agents` as an editable default agent template pack.
- R002: Represent each default team in its own folder with team metadata/settings and member folders.
- R003: Represent each default agent member with separate `instructions.md`, `settings.json`, and `skills.json`.
- R004: Preserve default agent keys, display names, purpose, provider choice, capabilities, workspace access, and configuration.
- R005: Review and improve each default agent instruction file with clear editable-template guidance while preserving role behavior.
- R006: Load default agents and teams from the template pack during seed creation.
- R007: Remove obsolete hardcoded default-agent instruction assets and default-agent literal blocks from production seed code after loader proof.
- R008: Update seed normalization so file-backed seeded teams are merged/refreshed consistently.
- R009: Add regression tests proving template pack loading and seeded team/member output.
- R010: Validate through targeted .NET commands plus Playwright/browser proof that agents work as before.
