---
            key: required-deliverables
            id: 40a50771-faed-50e0-bc71-8f7a153b300d
            name: Required Deliverables
            group: mission-scope
            blockKind: Delivery
            toolboxEligible: false
            recommended: true
            tags: artifacts, deliverables, outputs
            promptTypes: architecture, audit, plan, implementation, refactor, bugfix, review, testing, validation, performance, security, migration, embedded, ui
            blueprints: architecture-spec, repository-audit, implementation-plan, feature-implementation, safe-refactor, bugfix-with-regression-lock, validation-audit, ui-ux-delivery, embedded-firmware-iteration
            phases: planning, implementation, verification, delivery
            stackTags: 
            templateTokens: deliverable_1, deliverable_2, deliverable_3
            ---

            ## Required Deliverables
Produce the following deliverables in this session:
- {{deliverable_1}}
- {{deliverable_2}}
- {{deliverable_3}}

The session is not complete if the implementation is finished but the required artifacts are missing.
