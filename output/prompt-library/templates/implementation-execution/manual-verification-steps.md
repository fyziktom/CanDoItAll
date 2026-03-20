---
            key: manual-verification-steps
            id: 9f384c44-bc82-57ec-b435-b8d48a533710
            name: Implementation: Manual Verification Steps
            group: implementation-execution
            blockKind: Validation
            toolboxEligible: false
            recommended: false
            tags: handoff, manual-verification, qa
            promptTypes: implementation, bugfix, ui, embedded, validation
            blueprints: feature-implementation, bugfix-with-regression-lock, ui-ux-delivery, embedded-firmware-iteration, validation-audit
            phases: verification, delivery
            stackTags: 
            templateTokens: 
            ---

            ## Manual Verification
Provide manual verification steps for the changed behavior.

The steps should state:
- what to open or run,
- what input or action to perform,
- what the expected result should be,
- what logs, UI state, or artifacts should appear.
