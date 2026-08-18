# Acceptance evidence — SB07

| Acceptance area | Evidence path | Result | Notes |
|---|---|---|---|
| source ownership | `proof/SB07/architecture-change-record.md` | pass | Neutral presentation owns window/catalog/list/fields; Agent host owns effects and lifecycle. |
| architecture/dependencies | `proof/SB07/architecture-change-record.md` | pass | One-way references; no new cycle or blocking diagnostic. |
| implementation behavior | `proof/SB07/semantic-invariants.md` | pass | Labels, geometry, context, retention, and close semantics preserved. |
| impacted tests | `proof/SB07/impacted-tests-response.json` | pass | Focused 9/9 and required Components 990/990. |
| builds | `proof/SB07/manifest.json` | pass | Three affected production projects, zero warnings/errors. |
| source/phase guards | `proof/SB07/manifest.json` | pass | Boundary, phase, test-policy, neutral-source, and partial guards pass. |
| browser/UI parity | `proof/SB07/browser-parity.md` | pass | Real send/response, hide/reopen/history/affinity/settings/stop at 1600x1000. |
| requirements | `proof/SB07/manifest.json` | pass | All owned requirements closed. |
| checkpoint/progression | `reviews/CP3-settings-and-floating-review.md` | pass | CP3 passes to SB08. |

Owned requirements: UIR-055, UIR-060, UIR-061, UIR-062, UIR-063, UIR-064, UIR-073, UIR-075, UIR-077, UIR-078
