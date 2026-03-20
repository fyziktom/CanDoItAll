---
            key: mandatory-playwright-tests
            id: 1f589242-5113-5989-a64d-516d99a2b180
            name: Validation: Mandatory Playwright Tests
            group: validation-review
            blockKind: Testing
            toolboxEligible: false
            recommended: false
            tags: e2e, playwright, ui-tests
            promptTypes: implementation, bugfix, ui, testing, validation
            blueprints: feature-implementation, bugfix-with-regression-lock, test-strategy-and-automation, ui-ux-delivery, validation-audit
            phases: implementation, verification
            stackTags: 
            templateTokens: 
            ---

            ## UI End-to-End Tests
Add or update Playwright coverage for any user-visible flow changed by this work.

Focus on:
- critical happy paths,
- the regression being fixed,
- the main state transition that proves the UI is wired correctly.
