# Acceptance Gates

## Mandatory gates

1. Prepared bundle validator must pass before implementation.
2. Every critical gate must include source assertions, focused tests, anti-stub scan, and downstream dependency review.
3. Full build must pass before final closure.
4. Final source scan must prove no Process Core, no production driver API, no UI drift, no broad-host resurrection, and no forbidden viewport proof.
5. Completed-stage validator must pass.

## Do-not-pass conditions

- Any behavior-changing shortcut without explicit focused tests.
- Projection source-family order changes.
- Any removal of artifact projection families.
- Any hardcoded skip of process mock, workspace-written, existing-managed, response-text, browser, or completed-decision projection.
- Any replacement of current matching logic with a weaker string-only shortcut.
- Any new production driver API before the driver-readiness map is reviewed in a future bundle.
