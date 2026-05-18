# Cognitive Memory PostgreSQL Smoke Source Data

This folder contains source documents and mermaid mindmaps used to seed Cognitive Memory through the public development APIs. The data is intentionally stored outside automated tests.

## Files

- `sample-projects.json` is the API-load descriptor used by `Load-CognitiveMemorySamples.ps1`.
- `*.md` files are detailed source documents for each sample project.
- `*.mmd` files are mermaid mindmaps matching the project structures.

## Load Flow

1. Start `CanDoItAll.Web` in `Development`.
2. Create and activate a new PostgreSQL database profile.
3. Run `Load-CognitiveMemorySamples.ps1`.
4. Capture the generated evidence JSON under the bundle `evidence/` folder.

The loader creates project-structure projects and nodes first, adds the markdown/mindmap files as project assets, then calls Cognitive Memory ingestion and consolidation APIs.
