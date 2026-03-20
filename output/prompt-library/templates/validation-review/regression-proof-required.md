---
            key: regression-proof-required
            id: 48655845-7a4a-590d-89a0-5930fe1d0200
            name: Validation: Regression Proof Required
            group: validation-review
            blockKind: Validation
            toolboxEligible: false
            recommended: true
            tags: evidence, proof, regression
            promptTypes: bugfix, refactor, review, validation, testing
            blueprints: bugfix-with-regression-lock, safe-refactor, validation-audit, test-strategy-and-automation, senior-code-review
            phases: verification, delivery
            stackTags: 
            templateTokens: target_bug_or_risk
            ---

            ## Regression Proof
Provide regression proof for {{target_bug_or_risk}}.

That proof can be:
- an automated test,
- a fixture comparison,
- a screenshot, trace, or log,
- a targeted manual reproduction with a documented result.

Do not rely on "it should work now" reasoning.
