# Requirement Traceability

## Raw Audit Notes

| Raw note | Summary | Requirement IDs | Owning subbundle | Closure status |
|---|---|---|---|---|
| F01 | Runtime finalizer composition ignores effective mode. | R01 | 01 | Closed |
| F02 | Required-finalizer instructions conflict with JSON-schema response format. | R02 | 02 | Closed |
| F03 | Tool-policy middleware misclassifies ordinary tool exceptions as policy blocks. | R03 | 03 | Closed |
| F04 | Provider capability truth is split across core runtime, Workspace UI, registry, and DB seed flags. | R04, R05, R06 | 04 | Closed |
| F05 | Hardening tests are present but too static for critical invariants. | R08, R09 | 07 | Closed |
| F06 | No invariant prevents state-changing tools after finalizer call. | R07 | 05 | Closed |
| F07 | Typed `RunAsync<T>` path has not been evaluated. | Documentation evaluation | 06 | Closed |

## Requirement Owners

| Requirement | Owning subbundle | Proof target |
|---|---|---|
| R01 | 01 | Effective finalizer mode reaches runtime composition and all runtime paths preserve it. |
| R02 | 02 | Finalizer instructions are coherent with JSON-schema response format for required, shadow, and disabled modes. |
| R03 | 03 | Dedicated policy-block exception separates policy blocks from real tool failures. |
| R04 | 04 | Runtime provider capability truth is canonical and UI defaults align with it. |
| R05 | 04 | Workspace-backed provider registry does not contradict the core feature matrix. |
| R06 | 04 | Managed SQLite provider display and persisted metadata are reconciled or explicitly labeled. |
| R07 | 05 | Finalizer sequencing is observable and post-finalizer significant tools are handled by policy. |
| R08 | 07 | Behavioral tests cover runtime finalizer modes, policy exceptions, and provider truth. |
| R09 | 07 | Verification docs list actual commands and outcomes truthfully. |

## Closure Proof

| Requirement | Closure proof |
|---|---|
| R01 | `MafAgentRuntimeTests` passed and covered required, shadow, disabled, continuation, and retry finalizer-mode behavior. |
| R02 | `MafAgentRuntimeTests` passed and covered JSON-only required instructions, shadow at-most-once instructions, and disabled instruction omission. |
| R03 | `AgentToolInvocationPolicyTests` and static regression tests passed and covered dedicated policy-block exceptions plus preservation of ordinary tool exceptions. |
| R04 | `ProviderFeatureMatrixTests`, `SettingsPageProvidersTests`, and `WorkspaceProviderCapabilityIntegrationTests` passed and covered UI/core provider capability agreement. |
| R05 | `WorkspaceProviderCapabilityIntegrationTests` passed and covered persisted provider structured-output truth for OpenAI and Ollama save paths. |
| R06 | `ProviderFeatureMatrixTests` passed and covered managed SQLite OpenAI chat-completions structured-output source truth. |
| R07 | `AgentFinalizerPolicyTests` and `AgentFrameworkExecutionRunTrackingIntegrationTests` passed and covered post-finalizer significant-tool enforcement. |
| R08 | Focused unit, component, and integration tests passed for all round2 behavior surfaces. |
| R09 | `docs/agent-runtime-hardening-verification.md` records exact mandatory and focused command outcomes, including unrelated full-suite blockers. |
