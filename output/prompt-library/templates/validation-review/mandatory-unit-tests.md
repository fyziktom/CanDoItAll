---
            key: mandatory-unit-tests
            id: cf0d99d6-96d0-54ca-b216-287c26ca6477
            name: Validation: Mandatory Unit Tests
            group: validation-review
            blockKind: Testing
            toolboxEligible: false
            recommended: true
            tags: logic, testing, unit-tests
            promptTypes: implementation, refactor, bugfix, testing, validation, embedded
            blueprints: feature-implementation, safe-refactor, bugfix-with-regression-lock, test-strategy-and-automation, validation-audit, embedded-firmware-iteration
            phases: implementation, verification
            stackTags: 
            templateTokens: 
            ---

            ## Unit Tests
Add or update unit tests for the logic touched by this work.

The unit tests should:
- target the smallest stable unit that covers the behavior,
- lock in the failure mode being fixed or introduced,
- remain deterministic and fast.
