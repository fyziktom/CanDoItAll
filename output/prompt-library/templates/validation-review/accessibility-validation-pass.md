---
            key: accessibility-validation-pass
            id: 87edddae-452a-5259-a6ba-0dad5f41fbc2
            name: Validation: Accessibility Validation Pass
            group: validation-review
            blockKind: Validation
            toolboxEligible: false
            recommended: false
            tags: a11y, accessibility, ui
            promptTypes: ui, implementation, review, validation
            blueprints: ui-ux-delivery, validation-audit, feature-implementation, senior-code-review
            phases: verification, delivery
            stackTags: 
            templateTokens: target_ui_flow
            ---

            ## Accessibility Validation
Validate accessibility for {{target_ui_flow}}.

Check:
- keyboard reachability,
- focus order and visible focus,
- labels and semantics,
- fallback behavior when canvas or complex UI is involved.
