# Follow-up requirements

## R01 — Runtime finalizer composition must be mode-aware

Severity: Critical

The MAF runtime must not attach finalizer tools or exact-once finalizer instructions solely because a structured-output contract is present.

Acceptance criteria:

- Effective finalizer mode is resolved before runtime agent composition.
- Required mode attaches the finalizer tool and required instructions.
- Shadow mode attaches either optional/shadow finalizer instructions or no finalizer tool, depending on final design, but never required instructions.
- Disabled mode attaches no finalizer tool and no finalizer instructions.
- Initial runs, approval continuations, temperature retry paths, scenario harness runtime, and mock runtimes preserve the mode signal.
- Tests cover all three modes.

## R02 — Finalizer instructions must be consistent with JSON-schema response format

Severity: High

Finalizer instructions must not imply the final assistant response may be prose/Markdown when `ChatResponseFormat.ForJsonSchema(...)` is active.

Acceptance criteria:

- Required mode instructs the model to call the finalizer exactly once and then return one JSON object matching the same schema.
- Required mode forbids Markdown/prose in the final assistant response.
- Shadow mode clearly states which output is authoritative.
- Disabled mode adds no finalizer instructions.
- Tests verify the instruction text does not contain misleading “display-only assistant text” wording in required mode.

## R03 — Tool-policy blocks must use a dedicated exception type

Severity: High

Policy blocks must be distinguishable from real tool execution failures.

Acceptance criteria:

- Add `AgentToolPolicyBlockedException` or equivalent.
- Throw it only from policy-deny, skip-execution, and missing-effective-approval-path branches.
- Catch only this dedicated type for policy-block telemetry/wrapping.
- Remove broad `IsPolicyException(...) => InvalidOperationException or NotSupportedException` logic.
- Tests prove a tool-thrown `InvalidOperationException` is not reclassified as policy-blocked.

## R04 — Provider runtime capability truth must have a single source

Severity: High

Provider runtime capabilities must be derived from `ProviderFeatureMatrix` or one equivalent canonical service.

Acceptance criteria:

- Workspace UI defaults are derived from or aligned with the core matrix.
- Ollama defaults do not claim structured-output capability unless a real MAF JSON-schema path is implemented and tested.
- OpenAI/Azure Chat Completions structured-output capability is represented consistently in UI/display/runtime truth.
- Persisted DB flags are either removed from runtime decisions or clearly labeled as legacy/operator metadata.
- Tests cover OpenAI Responses, OpenAI Chat Completions, Azure variants if represented, and Ollama local/remote.

## R05 — Workspace-backed provider registry must not contradict core feature matrix

Severity: High

`WorkspaceBackedAgentProviderProfileRegistry` must not store `SupportsStructuredOutput` using transport-only logic that contradicts the runtime matrix.

Acceptance criteria:

- Replace `model.Transport == ProviderTransportKind.Responses` with canonical feature-matrix resolution or remove the flag from runtime relevance.
- UI display and provider registry data agree for OpenAI Chat Completions and Ollama.

## R06 — Managed SQLite provider display must be truthful

Severity: Medium

The managed SQLite OpenAI provider must not present misleading structured-output capability information.

Acceptance criteria:

- The core runtime provider profile and workspace provider DB metadata are reconciled.
- If persisted `SupportsStructuredOutput` remains false for compatibility, UI must show computed runtime capability separately.
- Documentation explains the distinction.

## R07 — Finalizer sequencing should be observable and optionally enforced

Severity: Medium

Required finalizer calls should happen after all state-changing/validation tool work.

Acceptance criteria:

- Runtime telemetry captures all tool-call sequence numbers and classifications.
- Required finalizer validation can detect mutation/validation/destructive tools after the finalizer call.
- At minimum, log a warning; preferably fail required governed runs when post-finalizer mutations occur.
- Tests cover finalizer-last invariant if enforcement is enabled.

## R08 — Hardening tests must be behavioral, not only static

Severity: Medium

Static source-string tests are useful but insufficient.

Acceptance criteria:

- Add behavior tests for finalizer mode-aware runtime composition.
- Add behavior or narrow integration tests for tool-policy exception boundary.
- Add UI/provider metadata tests for capability truth.
- Keep static tests only as supplementary guards.

## R09 — Verification documents must be truthful

Severity: Medium

Docs must not claim build/test success unless commands were actually executed in the repo environment.

Acceptance criteria:

- Verification docs list exact commands run.
- Include output summaries or failure reasons.
- If `dotnet` or SDK is unavailable, say so explicitly.
- Do not claim tests pass solely because test files exist.
