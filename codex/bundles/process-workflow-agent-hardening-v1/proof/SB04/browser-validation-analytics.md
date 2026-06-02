# SB04 Browser Validation Analytics

SB04 changed browser proof policy, schema validation, and runtime host receipts. It did not change a live application route or UI surface, so no real browser screenshot was generated in this subbundle. Live route capture is deferred to SB08 scenarios that run generated applications.

| Field | SB04 schema-level proof |
| --- | --- |
| Route | `/` in the positive validator fixture; absolute host `http://127.0.0.1:61234/` |
| Host | Runtime host record requires absolute HTTP/HTTPS host and rejects `http://127.0.0.1:61235/` when context expects `http://127.0.0.1:61234/` |
| DB profile | Validator fixture uses `process-db-profile` and `profile-sha256`; wrong id/fingerprint is rejected |
| Viewport | `1280x720`, bounded by validator to 320x240 through 7680x4320 |
| Playwright actions | `browser_navigate`, `browser_click`, `browser_press_key`, `browser_take_screenshot`, `browser_snapshot`, and `browser_console_messages` |
| Screenshot paths | Current-run paths under `artifacts/process-runs/<process-run-id>/browser/` |
| Console evidence | `browser_console_messages` output path under the same current-run browser root |
| Cleanup receipt | Current-run runtime startup receipt under `artifacts/process-runs/<process-run-id>/runtime/startup.json` |
| Result | Positive proof accepted; stale timestamp, copied output, wrong host, wrong DB profile, missing cleanup receipt, and foreign process-run evidence all rejected |

