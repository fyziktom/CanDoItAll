---
            key: architecture-validation-pass
            id: 7d65fc63-2d87-526e-84ce-9452bd920c3d
            name: Validation: Architecture Validation Pass
            group: validation-review
            blockKind: Validation
            toolboxEligible: false
            recommended: false
            tags: architecture-validation, boundaries, design
            promptTypes: validation, review, architecture, implementation
            blueprints: validation-audit, senior-code-review, architecture-spec, feature-implementation
            phases: verification, delivery
            stackTags: 
            templateTokens: 
            ---

            ## Architecture Validation
Validate that the implementation still matches the intended architecture.

Check:
- module boundaries,
- ownership of state and storage,
- contract shape,
- unwanted coupling or leakage between layers.
