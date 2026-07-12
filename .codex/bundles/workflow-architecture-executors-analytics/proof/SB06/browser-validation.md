# SB06 Large-Screen Browser Validation

## Session

- Application route: `/agents/workflows` (non-artifact local context).
- Viewport: 1600x1000, large desktop only.
- Browser: persistent Playwright MCP session against production DI and the running application.
- Current-session console errors: 0.
- Responsive scope: small and medium viewports were intentionally not tested, matching the user constraint.

## Actions And Assertions

1. Opened the Editor and inspected the production executor toolbox.
   - The toolbox reported 19 runnable executors.
   - The governed command executor remained visibly planned and was not presented as runnable.
   - Document to Markdown and Spreadsheet were discoverable.
2. Added Image generation.
   - Custom executors bypassed the generic creation dialog.
   - The trusted image renderer appeared immediately under Node setup, including its capability-aware provider setting.
3. Opened the Gmail plugin executor settings.
   - The plugin used its declarative schema fields, including connection, label, processed label, and maximum messages.
   - The desktop creation dialog rendered above toolbox/selection floating windows after the stacking-context repair.
4. Opened Analytics.
   - Token dimensions, known cost, unknown pricing/usage, duration, provider/model grouping, and recent runs were present from the typed query projection.
   - The empty token dataset remained explicit zero/unknown state rather than being misrepresented as missing UI.

## Browser-Found Repairs

- Custom-renderer executors now bypass the generic schema creation dialog so Node setup owns their configuration immediately.
- The desktop creation-dialog stacking context now wins over floating canvas windows.

## Screenshot Review

| Capture | Evidence | Visual review |
|---|---|---|
| Executor toolbox | `repo://workflow-executors-markdown.png` | Large-screen editor remains readable with the toolbox and selection windows open. The 19-executor count is visible, Markdown search is active, and Spreadsheet is shown without blocking overlap. |
| Trusted image settings | `repo://workflow-custom-image-settings.png` | Image generation is selected and its custom provider settings are immediately visible in Node setup. The canvas remains usable and the settings panel owns its scroll. |
| Gmail plugin settings | `repo://workflow-plugin-gmail-settings-fixed.png` | The schema dialog is above the floating windows, fields are aligned and readable, and the right-side definition panel remains stable. No blocking clipping or z-index conflict remains. |
| Workflow analytics | `repo://workflow-analytics-desktop.png` | Inventory, token/pricing, and duration sections have a clear hierarchy and readable metric cards at 1600x1000. Lower provider/model and recent-run sections remain reachable by the page scroll. |

## Result

`Pass`. The production desktop route exposes the new built-in and plugin executor settings paths and the typed analytics surface without current-session console errors or a blocking visual defect.
