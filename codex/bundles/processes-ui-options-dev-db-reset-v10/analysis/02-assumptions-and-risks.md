# Assumptions And Risks

## Assumptions

- "Development db" means the PostgreSQL database named `candoitall_development` configured for `CanDoItAll.Web` development.
- "Processes history, runs, etc" means process module definition/runtime tables and process-managed history, not agents, workflows, memory, projects, project structure, database profiles, or managed files.
- Reloading templates means importing/publishing the default process catalog from the current template pack after the process tables are cleared.

## Critical Path Risks

- If template projection still falls back for `Accountable`, `DecisionRecord`, `ApprovalRequired`, or `person-or-agent`, the reloaded development database will immediately contain weakened process contracts.
- If SQL cleanup targets the wrong table pattern, it could destroy agents, memory, projects, or project structure. SB02 must prove a process-prefix-only target list before executing.
- If the app or seeding host cannot resolve the active development profile, template reload may need to use a direct API call or a small scoped host utility.

## Validation Risks

- Component tests prove rendered options but not full browser layout. Browser proof is still expected unless local server startup is blocked.
- A broad solution build may expose unrelated warnings; closure should distinguish existing warnings from new failures.
- Process table truncation is destructive. Before/after counts must be captured as durable proof.

## Reopen Triggers

- Any unsupported non-empty template vocabulary found after SB01 requires reopening SB01 before database reload.
- Any SQL command that mentions a non-`Processes_` table requires stopping SB02 and rewriting the reset plan.
- Any post-reset count drop in non-process representative tables requires immediate blocker status and no closure.
- Any template reload failure caused by the new enum values requires reopening SB01.
