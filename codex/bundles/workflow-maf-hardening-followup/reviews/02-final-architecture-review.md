# Final architecture review

## Decision

The follow-up bundle can close. The implementation keeps the CanDoItAll workflow catalog as the canonical product model, updates the MAF package baseline to 1.8, compiles through the MAF adapter boundary, and hardens runtime behavior around HITL, approval, events, checkpoints, payloads, plugin governance, and backend honesty.

## Findings

- MAF package migration is no longer deferred: `Microsoft.Agents.AI`, `Microsoft.Agents.AI.OpenAI`, and `Microsoft.Agents.AI.Workflows` are on 1.8.0, with A2A packages aligned to `1.8.0-preview.260528.1`.
- `BindAsExecutor` remains the explicit adapter strategy for user-authored dynamic workflow graphs. Source-generated executors are reserved for future static workflow families with benchmark or Native AOT justification.
- HITL and approval gates are execution-position-aware: unreachable human nodes do not pause a run, and reached human/approval steps create explicit external requests.
- Workflow events now carry bounded typed envelopes with node, executor, request, source, and redacted payload metadata.
- Checkpoints are metadata-only in the in-process backend. Resume is explicitly unavailable until a durable backend owns trusted runtime state.
- Payload and artifact policy is centralized and applied to runtime events, inputs, outputs, executor failures, external requests, plugin logs, and tool receipts.
- Plugin workflow executor governance is deterministic: manifest validation enforces permission/capability consistency and plugin audit logging is an execution audit sink in a composite observer.
- Backend catalog and UI/API surfaces are honest: only `InProcess` is registered/runnable in the current host; `DurableTask` and `AzureFunctions` are planned/unavailable and cannot be saved, tested, or started as runnable production backends.

## Residual Risks

- Existing `MSB3277` Entity Framework Core Relational version conflict warnings remain outside this bundle. Owner: platform dependency cleanup.
- Durable production workflow execution is still not implemented. The current fix is honesty and hard failure for unavailable durable backends, not a durable runtime. Owner: future durable backend implementation.
- Resume remains `NotSupported` for metadata-only in-process checkpoints. Owner: future durable checkpoint/resume backend.
- No local `.github/workflows` directory is present. Owner: repository CI policy; SB08 records the expected local gate as restore/build plus targeted unit/integration/component regression.
- Source-generated MAF executors were not introduced. Owner: future performance/AOT work only if benchmarks or deployment constraints justify static workflow families.

## Validation

- Unit regression: `bundle://proof/SB08/unit-targeted-regression.txt`
- Integration regression: `bundle://proof/SB08/integration-targeted-regression.txt`
- Component regression: `bundle://proof/SB08/component-targeted-regression.txt`
- Final build: `bundle://proof/SB08/final-build.txt`
- Source assertions: `bundle://proof/SB08/source-assertions-risky-invariants.txt`
- Browser proof for UI-affecting SB07: `bundle://proof/SB07/browser-workflow-runtime-backends.json` and `bundle://proof/SB07/browser-workflow-runtime-backends-visible.png`
- Completed bundle validator: `bundle://proof/SB08/completed-bundle-validator.txt`
