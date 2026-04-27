# Normalized Requirements

| ID | Requirement | Source notes | Owning subbundle | Proof |
|---|---|---|---|---|
| REQ-01 | Add scoped `DialogService`, `TooltipService`, and upgraded `NotificationService` registrations through `AddCanDoItAllBaseLib`. | R1, R2 | `01-01-service-contracts-and-hosts` | Build and component tests. |
| REQ-02 | Add service host components that can be mounted once per app layout and render Tailwind-only overlay chrome. | R1, R2, R3 | `01-01-service-contracts-and-hosts` | Component tests and sandbox host proof. |
| REQ-03 | Preserve existing direct `Dialog` component API and existing notification callers. | R3 | `01-01-service-contracts-and-hosts` | BaseLib/Sandbox builds and existing tests. |
| REQ-04 | Implement dialog open/close APIs with task-returned objects, component content, inline render-fragment content, backdrop close control, and modal size options. | R1, R2, R5 | `02-02-dialog-service-behavior` | Component tests plus Playwright MCP dialog scenarios. |
| REQ-05 | Implement tooltip service behavior with open/close lifecycle, text and render-fragment content, positions, delays/durations, navigation-safe cleanup, and accessible host output. | R1, R2 | `03-03-tooltip-notification-services` | Component tests plus Playwright MCP open-state screenshot. |
| REQ-06 | Upgrade notification service with collection state, convenience overloads, dismiss/clear, duration, click/close callbacks, payload support, and accessible host output. | R1, R2 | `03-03-tooltip-notification-services` | Component tests plus Playwright MCP show/dismiss proof. |
| REQ-07 | Add sandbox examples in the established component sandbox structure for dialog, tooltip, and notification services. | R4 | `04-04-sandbox-docs-and-browser-proof` | Sandbox build and Playwright MCP route validation. |
| REQ-08 | Update docs describing setup, host placement, service usage, and validation commands. | R4 | `04-04-sandbox-docs-and-browser-proof` | Documentation review and traceability. |
| REQ-09 | Use Playwright MCP for browser validation, especially dialog modal sizing and object-return close paths. | R5 | `04-04-sandbox-docs-and-browser-proof` | Browser analytics rows with screenshots. |
| REQ-10 | Use the CanDoItAll bundle workflow for preparation, validation, execution, and final closure. | R6 | all | Prepared/completed bundle validators and execution report. |

## Scope Boundaries

- Radzen is a design reference only; no Radzen source, CSS, package reference, or runtime dependency should be copied into BaseLib.
- Side dialogs, drag/resize, and JS element-rect tooltip positioning are not required unless implementation reality makes them necessary for the requested proof.
- Product pages do not need to migrate direct dialog usage in this bundle; the new service API is additive.
