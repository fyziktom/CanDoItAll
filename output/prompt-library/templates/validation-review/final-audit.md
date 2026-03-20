---
            key: final-audit
            id: b7b0fbcc-096a-5d48-b069-b717c9e27005
            name: Validation: Final Audit
            group: validation-review
            blockKind: Validation
            toolboxEligible: false
            recommended: true
            tags: completion, final-audit, quality-gate
            promptTypes: architecture, audit, plan, implementation, refactor, bugfix, review, testing, validation, performance, security, migration, embedded, ui
            blueprints: validation-audit, feature-implementation, safe-refactor, bugfix-with-regression-lock, ui-ux-delivery, embedded-firmware-iteration
            phases: verification, delivery
            stackTags: 
            templateTokens: 
            ---

            ## Final Audit
Perform a final audit before declaring completion.

Confirm:
- the success criteria are truly met,
- the highest-risk regressions have proof,
- the deliverables are present,
- the remaining gaps are named honestly.
