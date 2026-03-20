---
            key: known-gaps-log
            id: a53983ec-812b-50dc-a25d-fb43c3de44df
            name: Workflow: Known Gaps Log
            group: workflow-orchestration
            blockKind: Delivery
            toolboxEligible: false
            recommended: false
            tags: deferred-work, known-gaps, risk
            promptTypes: architecture, audit, plan, implementation, refactor, bugfix, review, testing, validation, performance, security, migration, embedded, ui
            blueprints: architecture-spec, implementation-plan, feature-implementation, safe-refactor, validation-audit, embedded-firmware-iteration
            phases: planning, implementation, verification, delivery
            stackTags: 
            templateTokens: 
            ---

            ## Known Gaps
Keep a short known-gaps list for items that are deliberately postponed.

For each gap, note:
- why it was not completed now,
- what risk it creates,
- what event should bring it back into scope.
