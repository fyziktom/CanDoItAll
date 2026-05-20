# Assumptions And Risks

## Working Assumptions

- Documentation changes can be validated with file coverage and build checks; no browser proof is required because no Blazor UI behavior changes.
- The phrase "each project" means every tracked `.csproj` directory, including test and tool projects.
- Public-version preparation should reduce stale setup paths, not remove historical execution bundles or architecture records that still serve traceability.
- Existing Markdown style should stay lightweight: short purpose, responsibilities, dependencies, and validation commands.

## Critical Path Risks

- If the project inventory is wrong, the final README coverage claim becomes false and downstream docs stay stale.
- If setup docs describe an old SQLite-first workflow, new contributors will run the app against the wrong persistence profile.
- If MCP setup docs do not mention removal of stale Process/ProjectStructure MCP sections, public users can follow dead paths.

## Validation Risks

- `dotnet build CanDoItAll.slnx --no-restore` can fail for reasons unrelated to docs if sibling repositories or restored assets are missing.
- Markdown-only changes do not have compiler enforcement; source-grounded review and project README coverage checks are the primary proof.
- The bundle validator can pass structure while the docs remain semantically weak, so final review must compare claims against scripts and config.

## Reopen Triggers

- Reopen subbundle 01 if any additional `.csproj` without README is found after docs are edited.
- Reopen subbundle 02 if a setup command in README does not match a real script, Docker service, appsettings entry, or launch profile.
- Reopen subbundle 03 if the project coverage check reports any missing README.
- Reopen closure if retired MCP setup commands appear as active guidance.
