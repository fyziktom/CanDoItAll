# SB09 Browser Analytics

## SB04

The SB04 proof-quality checker validated that all five generated app scenarios have desktop and mobile browser summary rows, rendered body text, interactive controls, and no blocking browser failures.

Proof:

- `bundle://proof/SB04/scenarios/*/browser/browser-validation-summary.json`
- `bundle://proof/SB09/transcripts/proof-quality-new-sb04-pass.txt`

## SB08

The SB08 live UI proof captured:

- Process live list.
- Process detail.
- Process steps tab.
- Step detail.
- Workflow selection window.
- Workflow executor editor.

Viewports:

- Desktop: 1440x1000.
- Mobile: 390x844.

Diagnostics:

- Console errors: 0 in both viewports.
- Page errors: 0 in both viewports.
- Failed responses: 0 in both viewports.
- Failed requests: one expected Blazor disconnect abort in each viewport during teardown.

Summary:

- `bundle://proof/SB08/browser/browser-validation-summary.json`
