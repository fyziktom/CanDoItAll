# Risks and mitigations

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Overbuilding a full ERP instead of a focused CRM/HR module | High | Keep payroll, benefits, and marketing automation explicitly out of scope. |
| Duplicating people across CRM, HR, Projects, and AI layers | High | One Party root plus role/profile model; duplicate merge in B03. |
| Breaking existing Workbench participant behavior | High | Projection model with `PartyId` references and backward-compatible metadata changes. |
| Canvas leakage into business UI | Medium | Bundle rule: BaseLib only for CRM/HR pages; validator and acceptance criteria enforce it. |
| Sensitive HR content leaking into global search | High | Separate confidential notes and selective search indexing in B12. |
| Project pages remain relationship-blind even after module launch | High | B10 explicitly updates project summaries, filters, and assignment displays. |
| AI agents become a third disconnected registry | Medium | B09 binds AI-agent parties to existing Workspace provider profiles. |
| Too many schema changes make the bundle hard to implement | Medium | Stable relational core plus targeted JSON extension points. |
| Insufficient automated coverage for UI-heavy flows | High | B13 plus per-item Playwright requirements and semantic screenshot review. |
| Performance degrades with larger directories | Medium | Paged queries, indexed filters, and no giant all-record detail loads. |
| Automation hooks remain theoretical only | Low | B11 includes reminder-style jobs visible in Automation workspace. |
| Codex implementation drifts because prompts are vague | High | Each subbundle has file refs, prompts, ASCII layouts, acceptance criteria, and screenshot requirements. |
