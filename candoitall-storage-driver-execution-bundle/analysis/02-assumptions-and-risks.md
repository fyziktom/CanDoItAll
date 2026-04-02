
# Assumptions And Risks

## Working Assumptions

- The storage catalog should live in application data so it can participate in normal workspace/project settings and runtime profile switching.
- Bootstrap local filesystem behavior should remain available through a default seeded local storage record derived from current workspace settings.
- Existing `IFileStore` and `IManagedArtifactStore` callers need a compatibility seam so execution can proceed phase-by-phase instead of as a risky big-bang rewrite.
- The first provider set is FileSystem + IPFS + FTP, but the abstraction should be open for additional providers without module rewrites.
- UI components should be split between reusable presentational pieces and module-specific orchestration so the shared codebase stays maintainable.

## Critical Path Risks

- If Phase 01 chooses the wrong object-reference and persistence model, every downstream module adoption becomes untrustworthy and costly to reopen.
- If Phase 02 leaves preview/download/local-open behavior tied to relative filesystem paths, Phase 04 browser proof will be misleading because remote providers will still fail in real usage.
- If the compatibility seam is weak, migration will stall mid-flight with some callers using old semantics and others using new semantics.
- If storage-node modeling in project structure is underspecified, users will not be able to attach multiple storages cleanly or derive subtree defaults.

## Validation Risks

- FTP real-protocol proof may be blocked by environment limitations; that must stay explicitly blocked instead of being inferred from unit tests.
- Manual Playwright MCP proof can be skipped if the bundle does not force screenshot logging and written visual findings.
- UI regressions around modal overlays, dropdowns, and preview panes are easy to miss if only automated assertions are used.
- Migration coverage can look green while PostgreSQL and SQLite snapshots drift; both migration projects must be reviewed explicitly.

## Reopen Triggers

- Reopen Phase 01 if later module adoption needs fields or provider/object semantics that the contract model cannot represent cleanly.
- Reopen Phase 02 if any changed UI still depends on raw `MediaRelativePath` or `/managed-files` assumptions for non-filesystem providers.
- Reopen Phase 03 if a later UI or host-action change lands without a matching automated test extension or manual MCP proof row.
- Reopen Phase 04 if the XLSX touchpoint audit finds an in-scope row with no owner, no checklist coverage, or no proof path.
