# Assumptions And Risks

## Assumptions

- The manager-run app remains available for the full validation loop.
- The configured MCP agent token and capability settings allow local mutation of the target project without separate manual approval.
- Creating validation subprojects and nodes under `CanDoItAll Main` is acceptable because the user explicitly requested live transfer into that project.
- The imported source can be represented by a combination of subprojects, project blocks, work items, files, repositories, environments, and notes without inventing new domain types.

## Critical Path Risks

- If project lease acquisition fails or mutation approval is required, the live validation path will stop before meaningful import proof exists.
- If the XMind import path accepts the archive but produces an unusable flat tree, later semantic shaping work may become much larger than intended.
- If browser proof shows a mismatch between MCP data and the structure UI, the validation bundle must reopen earlier phases before closure.

## Validation Risks

- Another stale Codex session may still be bound to an older or broken MCP process, which could reproduce the user’s earlier “cannot access information” symptom even if this session succeeds.
- The lack of an exposed MCP analytics-query tool may force analytics capture through the HTTP API instead of the MCP surface itself.
- The bundle must distinguish between defects in the MCP server and defects in session reload or client configuration, otherwise the defect capture will be misleading.

## Reopen Triggers

- Reopen subbundle 01 if live mutation reveals the node-type mapping was too shallow or contradicted by the actual project-structure capabilities.
- Reopen subbundle 02 if project or repo-branch lease proof is weak, missing, or contradicted during later mutation.
- Reopen subbundle 03 if any created structure cannot be read back correctly through both MCP and browser UI.
- Reopen subbundle 04 if any raw note remains only partially solved or if analytics and checklist proof are missing.
