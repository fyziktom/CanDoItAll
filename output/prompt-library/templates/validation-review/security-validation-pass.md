---
            key: security-validation-pass
            id: fafc2173-13c5-5d33-9aaf-69ccf319daac
            name: Validation: Security Validation Pass
            group: validation-review
            blockKind: Security
            toolboxEligible: false
            recommended: false
            tags: review, security, threats
            promptTypes: security, implementation, review, validation
            blueprints: security-hardening, feature-implementation, senior-code-review, validation-audit
            phases: verification, delivery
            stackTags: 
            templateTokens: target_change
            ---

            ## Security Validation
Perform a security-focused review of {{target_change}}.

Look for:
- secret leakage,
- unsafe storage or transport,
- injection or validation gaps,
- authz or authn regressions,
- excessive trust in client-side inputs.
