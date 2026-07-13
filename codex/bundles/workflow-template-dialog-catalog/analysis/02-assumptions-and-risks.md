# Assumptions And Risks

## Assumptions

- The Workflows page remains the ownership boundary; no new API endpoint is required unless component tests prove page-level logic becomes untestable.
- The existing `WorkflowTemplatePackLoader` can remain scoped and invoked on demand by the page/dialog.
- User-owned draft copies should not contain the managed seed marker in their description.
- Generic offer-analysis templates can keep the same graph shape while changing names, descriptions, routing instructions, and asset titles.
- Small and medium layout validation is intentionally skipped because the user stated the app is large-screen-only.

## Critical Path Risks

- If lazy loading is only cosmetic, the template pack could still load during initialization through an accidental computed property or tab preload.
- If the preview canvas reuses the full editor without guardrails, users may get edit affordances inside a preview-only flow.
- If draft naming only checks exact names, repeated "Add to my drafts" actions could collide or produce unstable naming.
- If SEAMARK debranding changes only display names, sensitive company/pricing details may remain in routing instructions or output titles.

## Validation Risks

- bUnit/component tests can prove state and markup, but not actual dialog sizing, clipping, or canvas readability.
- Playwright screenshots can prove large-screen open states, but generated design proposals cannot be treated as shipped proof.
- The component MCP was unavailable during preparation; implementation must rely on existing component usage and source inspection unless the MCP comes back.

## Reopen Triggers

- Reopen SB02 if Playwright shows the catalogue dialog clips, loads templates before opening, or no longer fits large-screen workflow.
- Reopen SB03 if "Add to my drafts" creates a managed example, an active workflow, or a duplicate name without the required numeric prefix.
- Reopen SB04 if any UI-facing template or test fixture still contains `SEAMARK`, exact company-sensitive price facts, or company-specific source names.
- Reopen earlier UI subbundles if final screenshot comparison finds major divergence from the generated proposals that affects usability.
