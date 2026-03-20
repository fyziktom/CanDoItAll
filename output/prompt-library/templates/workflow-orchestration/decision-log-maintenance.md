---
            key: decision-log-maintenance
            id: 696c25ef-6044-528b-92af-446ed6142247
            name: Workflow: Decision Log Maintenance
            group: workflow-orchestration
            blockKind: Delivery
            toolboxEligible: false
            recommended: false
            tags: adr, continuity, decisions
            promptTypes: architecture, audit, plan, implementation, refactor, bugfix, review, testing, validation, performance, security, migration, embedded, ui
            blueprints: architecture-spec, implementation-plan, feature-implementation, safe-refactor, bugfix-with-regression-lock, validation-audit, embedded-firmware-iteration
            phases: architecture, planning, implementation, verification, delivery
            stackTags: 
            templateTokens: 
            ---

            ## Decision Log
Record decisions that affect future work:
- what decision was made,
- why it was chosen,
- what tradeoff or limitation remains,
- what evidence supports the choice.

Keep the log short and operational, not essay-like.
