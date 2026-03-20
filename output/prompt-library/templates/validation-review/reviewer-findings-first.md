---
            key: reviewer-findings-first
            id: a456a3c4-01c3-57a9-9517-eca806cdf911
            name: Validation: Reviewer Findings First
            group: validation-review
            blockKind: Validation
            toolboxEligible: false
            recommended: true
            tags: findings-first, review, risk
            promptTypes: review, validation, security, performance
            blueprints: senior-code-review, validation-audit, security-hardening, performance-hardening
            phases: verification, delivery
            stackTags: 
            templateTokens: 
            ---

            ## Review Output Style
If this session is a review, present findings first.

Order them by severity and include:
- the risky behavior or flaw,
- where it lives,
- why it matters,
- what evidence is missing or what should change.

Keep the overall summary short and secondary.
