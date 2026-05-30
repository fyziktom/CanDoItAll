# SB03 Semantic Invariants

## Invariant SB03-DOCS-CLOSURE

- Invariant ID: `SB03-DOCS-CLOSURE`
- Source raw note: `N001` required proper README/docs in the new MCP repo and assurance that MCPs build and reinstall.
- Expected behavior: The MCP repo documents purpose, project inventory, build/test commands, resetup ownership, settings, artifacts, and retired MCPs; current main-repo docs and skills no longer direct users to obsolete in-repo MCP source paths.
- Disallowed shallow implementation: Adding only a placeholder README or leaving setup instructions pointing at old `tools/CanDoItAll.Mcp.*` paths in the main repo.
- Failing-first test: N/A - process/non-production documentation closure; no application production behavior changed. Negative proof is the docs assertion transcript that rejects obsolete current-doc source references.
- Passing test: `bundle://proof/SB03/transcripts/docs-and-final-assertions.txt`.
- Changed source files: MCP repo local context only: `README.md`, `docs/server-inventory.md`, `docs/build-test-and-resetup.md`, `docs/settings-and-artifacts.md`; main repo docs and skills updated under `repo://README.md`, `repo://docs`, `repo://.github/copilot-instructions.md`, and `repo://codex/skills/candoitall-dotnetwatch-setup`.
- Production assertions: N/A - documentation and validation closure only.
- Red-team negative case: `bundle://proof/SB03/transcripts/docs-and-final-assertions.txt` fails if the MCP README lacks resetup/artifact/skill guidance or current main-repo docs/skills retain obsolete source-path references.
- Downstream dependency check: Final completed-stage validation requires `SB01`, `SB02`, and `SB03` statuses and proof to agree.
