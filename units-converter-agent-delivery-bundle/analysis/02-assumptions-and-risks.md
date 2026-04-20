# Assumptions And Risks

## Assumptions

- The active database-profile id should remain the canonical organization-scope key going forward, even if legacy organization-scope folders must be merged into it.
- There should be only one canonical editable AgentFramework organization catalog per database profile workspace.
- The serious units-converter project may reuse CanDoItAll services or scenario tooling for repeatability, as long as the resulting agents, projects, processes, assignments, and runs are real CanDoItAll records and not fake fixture-only projections.
- An enabled OpenAI provider can be resolved or created in the target profile before the serious run begins.

## Critical Path Risks

- If organization-scope reconciliation is wrong, CRM-HR and AgentFramework will continue to diverge and every downstream process-assignment or agent-edit path will remain untrustworthy.
- If Playwright or screenshot-proof capability is present only in baseline agents and not in the serious delivery agents, QA proof will look correct on paper but fail during the real run.
- If serious-project provisioning still depends on showcase-specific names or artifact paths, the rerun will not actually prove a reusable delivery architecture.

## Validation Risks

- A live serious run can expose issues that are invisible in tests, especially around runtime assignment resolution, launch approval, artifact routing, and browser-proof import.
- Migrating legacy organization-catalog data can leave stale bindings, duplicate parties, or artifact paths if the merge logic only fixes counts instead of full identity ownership.
- Project structure proof is only trustworthy if run outputs, progress changes, and file-output nodes are visible in the structure after execution, not just written to disk.

## Reopen Triggers

- Reopen subbundle `01` immediately if `/agents` and `/crm-hr/agents` show different real-agent populations after the ownership repair.
- Reopen subbundle `02` immediately if a serious-run agent lacks `playwright-local-mcp`, cannot invoke browser tools, or cannot explain screenshot evidence.
- Reopen subbundle `03` immediately if the units-converter project or its phase/process setup still contains showcase naming or hardcoded scenario-only assumptions.
- Reopen subbundle `04` or `05` immediately if live execution reveals missing project-structure progress, missing output-node traceability, or artifact handoffs that succeed only by manual patching.
