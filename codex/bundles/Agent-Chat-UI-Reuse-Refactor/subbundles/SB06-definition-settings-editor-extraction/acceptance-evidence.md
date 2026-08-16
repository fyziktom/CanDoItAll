# Acceptance evidence — SB06

| Acceptance area | Evidence path | Result | Notes |
|---|---|---|---|
| source ownership | `proof/SB06/architecture-change-record.md`, `semantic-invariants.md` | pass | Neutral editor/identity/provider/model presentation moved; Agent services, persistence, policy, and tabs remain owned by AgentFramework. |
| architecture/dependencies | `proof/SB06/architecture-change-record.md`, `components-mcp.json` | pass | Opaque contracts and one-way Module -> Agent Components -> neutral dependency; no new project cycle or blocking diagnostic. |
| implementation behavior | `proof/SB06/manifest.json`, focused test executions | pass | Labels, bindings, defaults, override, disabled/validation/avatar slots, mapping, and Agent regressions pass. |
| impacted tests | `proof/SB06/impacted-tests-request.json`, `impacted-tests-response.json` | pass | CodeAnalytics required AllSuppliedSuites; 981/981 Components tests passed, with nonzero focused discovery. |
| builds | `proof/SB06/manifest.json` | pass | Neutral UI, Agent Components, Agent module, test assembly, and isolated Web host passed with zero warnings/errors. |
| source/phase guards | `proof/SB06/manifest.json` | pass | Repository boundary, phase exclusion, neutral forbidden-source, partial-growth, and diff inspection pass. |
| browser/UI parity | `proof/SB06/browser-parity.md`, `proof/SB06/browser/*` | pass | Identity, Runtime, and Capabilities render with preserved controls/tab order; zero console errors/warnings. |
| requirements | `proof/SB06/manifest.json`, `semantic-invariants.md` | pass | UIR-050/051/052/053/054/073/075/077 closed for this slice. |
| checkpoint/progression | `STATUS.json`, `SESSION-HANDOFF.md` | pass | No named checkpoint; behavioral gate passes to SB07. |

Owned requirements: UIR-050, UIR-051, UIR-052, UIR-053, UIR-054, UIR-073, UIR-075, UIR-077
