# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| `Use components MCP and Chat page example for fit-to-window containment.` | `analysis/01-current-state.md`, `architecture/01-target-solution.md` | `subbundles/01-process-workspace-shell-and-tab-containment` | Targeted component assertions plus live `/processes` browser review | Chat page is a reference pattern, not an implementation target. |
| `Make Process definitions cards list scrollable inside.` | `requirements/01-normalized-requirements.md` | `subbundles/01-process-workspace-shell-and-tab-containment` | Component markup assertions and browser scroll-region proof | Must stay inside the list pane. |
| `Same for content of tabs.` | `requirements/01-normalized-requirements.md` | `subbundles/01-process-workspace-shell-and-tab-containment` | Component assertions for fill-height tabs plus browser review of selected panels | Critical foundation for downstream modal trust. |
| `Same for the modal with Templates. List must be scrollable same as content.` | `requirements/01-normalized-requirements.md` | `subbundles/02-template-library-dialog-and-mermaid-viewport-containment` | Playwright open-modal proof with screenshot review | Must avoid weak nested-scroll behavior. |
| `Assure that mermaid graph during zoom will not overflow the component.` | `requirements/01-normalized-requirements.md` | `subbundles/02-template-library-dialog-and-mermaid-viewport-containment` | Playwright zoom interaction plus screenshot and DOM geometry review | Screenshot already shows the current failure mode. |
| `Solve it as bundle.` | `README.md`, `plan/01-phase-plan.md`, `reviews/01-execution-report.md` | `subbundles/03-browser-proof-and-bundle-closure` | Bundle validators and synchronized execution report | No exception. |
