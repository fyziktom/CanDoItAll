---
            key: performance-validation-pass
            id: 8cbb6457-11ce-5ab9-a77b-d35ea01d5c4e
            name: Validation: Performance Validation Pass
            group: validation-review
            blockKind: Validation
            toolboxEligible: false
            recommended: false
            tags: hot-path, latency, performance
            promptTypes: performance, implementation, review, embedded, ui
            blueprints: performance-hardening, feature-implementation, validation-audit, embedded-firmware-iteration, ui-ux-delivery
            phases: verification, delivery
            stackTags: 
            templateTokens: target_change
            ---

            ## Performance Validation
Validate the performance impact of {{target_change}}.

Measure or reason explicitly about:
- critical hot paths,
- allocations or payload size,
- latency or scheduling impact,
- changes that could degrade mobile, browser, or embedded environments.
