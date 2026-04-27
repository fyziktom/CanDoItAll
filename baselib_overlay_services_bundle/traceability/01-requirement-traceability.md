# Requirement Traceability

| Raw note | Exact wording summary | Requirements | Owning subbundle | Planned proof | Exception |
|---|---|---|---|---|---|
| R1 | Missing proper DialogService, TooltipService, NotificationService. | REQ-01, REQ-02, REQ-04, REQ-05, REQ-06 | `01`, `02`, `03` | Builds, component tests, sandbox proof. | None. |
| R2 | Radzen is a good example; use Tailwind styles only. | REQ-02, REQ-04, REQ-05, REQ-06 | `01`, `02`, `03` | Source review, no Radzen deps, Tailwind output. | No Radzen CSS or full feature parity; Radzen is reference only. |
| R3 | Existing component structure is good. | REQ-03 | `01` | Existing builds/tests and preserved direct APIs. | None. |
| R4 | Add examples in sandbox and update docs. | REQ-07, REQ-08 | `04` | Sandbox build, docs diff, Playwright route. | None. |
| R5 | Must validate with Playwright MCP, especially dialog cases. | REQ-09 | `04` | Browser analytics rows and screenshots. | None. |
| R6 | Use bundle workflow. | REQ-10 | all | Prepared and completed validators plus execution report. | None. |

## Destination Map

| Requirement | Bundle destinations | Source references |
|---|---|---|
| REQ-01 | `architecture/01-target-solution.md`, `subbundles/01-01-service-contracts-and-hosts` | BaseLib services and DI files. |
| REQ-02 | `architecture/01-target-solution.md`, `plan/01-phase-plan.md`, `subbundles/01-01-service-contracts-and-hosts` | BaseLib host components. |
| REQ-03 | `analysis/02-assumptions-and-risks.md`, `subbundles/01-01-service-contracts-and-hosts` | Existing `Dialog.razor`, `Notification.razor`, tests. |
| REQ-04 | `subbundles/02-02-dialog-service-behavior` | Radzen dialog references and BaseLib dialog files. |
| REQ-05 | `subbundles/03-03-tooltip-notification-services` | Radzen tooltip references and BaseLib tooltip files. |
| REQ-06 | `subbundles/03-03-tooltip-notification-services` | Radzen notification references and BaseLib notification files. |
| REQ-07 | `subbundles/04-04-sandbox-docs-and-browser-proof` | Sandbox overlays/feedback files. |
| REQ-08 | `subbundles/04-04-sandbox-docs-and-browser-proof` | BaseLib and sandbox README files. |
| REQ-09 | `reviews/01-execution-report.md` | Playwright MCP evidence. |
| REQ-10 | Whole bundle | Bundle validator outputs. |
