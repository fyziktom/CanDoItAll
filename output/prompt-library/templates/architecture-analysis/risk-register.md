---
            key: risk-register
            id: dcbbe6ad-6260-5d8e-82bd-04e1f8aad5d3
            name: Architecture: Risk Register
            group: architecture-analysis
            blockKind: Validation
            toolboxEligible: false
            recommended: true
            tags: mitigation, planning, risk
            promptTypes: architecture, plan, review, validation, security, performance, embedded
            blueprints: architecture-spec, implementation-plan, validation-audit, security-hardening, performance-hardening, embedded-firmware-iteration
            phases: architecture, planning, verification
            stackTags: 
            templateTokens: target_feature_or_change
            ---

            ## Risk Register
List the main risks for {{target_feature_or_change}}.

For each risk, capture:
- the failure mode,
- why it matters,
- the mitigation,
- the validation that will prove the mitigation worked.
