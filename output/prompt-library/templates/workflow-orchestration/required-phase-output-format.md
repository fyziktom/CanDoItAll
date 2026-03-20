---
            key: required-phase-output-format
            id: 010a91e9-43d8-514a-9e61-37ca1ab85b55
            name: Workflow: Required Phase Output Format
            group: workflow-orchestration
            blockKind: Delivery
            toolboxEligible: false
            recommended: true
            tags: handoff, output-format, phase-summary
            promptTypes: architecture, audit, plan, implementation, refactor, bugfix, review, testing, validation, performance, security, migration, embedded, ui
            blueprints: architecture-spec, implementation-plan, feature-implementation, safe-refactor, validation-audit, embedded-firmware-iteration, ui-ux-delivery
            phases: architecture, planning, implementation, verification, delivery
            stackTags: 
            templateTokens: 
            ---

            ## Phase Output Format
At the end of the phase, provide:
1. what you inspected,
2. what you changed or produced,
3. what validation you ran,
4. remaining risks or blockers,
5. the recommended next step.

Keep the format consistent so later agents and reviewers can diff progress quickly.
