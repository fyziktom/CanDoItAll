# Impact Matrix

Legend:

- **Confirmed** — directly observed in the pinned branch or upstream source.
- **Inference** — strongly implied by observed construction; SB01 must prove it.
- **Discovery** — no confirmed use in inspected paths.

| MAF change | CanDoItAll exposure | Impact | Required action | Priority | Confidence |
|---|---|---|---|---:|---|
| Stable packages move 1.13 → 1.15 | Three direct stable references | Compile/runtime | Align through one shared stable version property | P0 | Confirmed |
| A2A matching preview build | Main adapter and hosting direct references; host registers A2A | Compile/protocol | Use `1.15.0-preview.260722.1` in both locations; smoke test | P0 | Confirmed |
| Approval-response binding enabled by default | `ChatClientAgentOptions` does not opt out in inspected construction | Security/state | Keep enabled; test effective provider pipeline and cross-version state | P0 | Inference |
| 1.13 pending session lacks binding state | Custom approval persistence/rehydration | Runtime/security | Require native 1.15 serialized state; drain/reissue legacy state without reconstruction | P0 | Confirmed |
| Non-approval-required bypass now enabled by default | Current 1.13 code did not explicitly enable old option | Behavioral | Explicitly disable for parity, then adopt in separate phase | P0 | Confirmed |
| `ToolApprovalAgent` API stabilization/signature change | No confirmed direct usage | Compile/optional | Grep and migrate only matches | P1 | Discovery |
| Terminal workflow output preference | Handoff workflow is hosted as an agent | Correctness | Compare direct and full streaming paths; revise projection | P0 | Confirmed |
| Workflow message ordering fix | Handoff emits intermediate tool calls/results | Correctness/history | Add adjacency/order fixtures; remove duplicate ordering hacks only after proof | P0 | Confirmed |
| New `MessageMerger` behavior | Runtime performs an additional MEAI merge | Correctness | Audit snapshot/merge code and resolved MEAI semantics | P0 | Confirmed |
| Workflow session assembly identity tolerance | Custom workflow checkpoint bridge is registered | Recovery | Determine whether native external envelopes are persisted; cross-version fixture | P1 | Inference |
| `ChatClientAgentSession` strict JSON fix | Opaque session serialization/deserialization | Recovery | Cross-version fixture; no custom workaround removal without proof | P1 | Confirmed |
| Compaction summary fix | No compaction use observed in targeted paths | Optional | Grep; test only if active | P2 | Discovery |
| Harness file access becomes opt-in | Custom workspace/file services are confirmed | No direct impact unless Harness exists | Grep all Harness APIs; do not replace custom tools | P1 | Confirmed/Discovery |
| Harness/FileMemory stabilization | Rich custom memory and tool architecture exists | Optional | Evaluate only for isolated scratch/harness scenarios | P3 | Discovery |
| Message injection stabilization | Transient context currently uses context providers | Optional | Evaluate later for in-loop recovery messages | P3 | Discovery |
| AG-UI split | No direct package observed | Compile only if hidden use | Grep `AGUI`, `AddAGUI`, `MapAGUI` | P2 | Discovery |
| Declarative `autoSend` fix | No declarative package observed | Optional | Grep; add test only if used | P2 | Discovery |
| OpenAI Responses hosting helpers | Runtime uses OpenAI Responses as a provider, not confirmed as a hosted Responses server | No first-pass replacement | Inventory hosting package implementations; defer redesign | P3 | Confirmed/Discovery |
| Shell UTF-8 fix | Custom command execution is confirmed; MAF Harness shell not confirmed | No direct impact unless Harness/CodeAct exists | Grep; keep custom command tests | P2 | Discovery |
| LocalCodeAct hardening | No confirmed use | Security if present | Grep and test aliases/import restrictions | P2 | Discovery |
| Cosmos TTL fix | No confirmed Cosmos history provider | No impact unless hidden use | Grep | P3 | Discovery |
| Logging allocation fix | Workflow resume may be used | Performance | No code change expected; compare telemetry only | P3 | Inference |
| Stable API warning changes | Project-level `MAAI001`/`MAAIW001` suppression exists | Maintainability | Temporarily inventory warnings; narrow suppressions | P1 | Confirmed |
