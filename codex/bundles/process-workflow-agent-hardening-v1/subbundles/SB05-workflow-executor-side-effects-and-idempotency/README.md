# SB05 - Workflow Executor Side Effects And Idempotency

## Status

Ready for implementation. Classification: **Critical foundation**.

## Objective

Harden workflow executor lifecycle for external systems, especially Office365/Gmail email workflows, so discovery, preview, dry-run, commit, idempotent retry, duplicate prevention, processed markers, and unavailable executors are explicit.

## Covered Inputs

Covers Office365 category workflow side effects, processed-category mutation, Gmail catalog-visible-but-unavailable state, executor catalog consistency, dry-run safety, and idempotent scheduler execution.

## Prerequisites

SB01 completed. Do not rerun the original evidence mailbox category unless using a dedicated controlled test category.

## Exact Source References

- `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs`
- `repo://src/plugins/CanDoItAll.Plugin.Gmail/GmailWorkflowExecutor.cs`
- `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365BundledPlugin.cs`
- `repo://src/plugins/CanDoItAll.Plugin.Gmail/GmailBundledPlugin.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs`
- `repo://codex/bundles/chatgpt-pro-process-workflow-agent-hardening-inputs-v1/inputs/api-captures/workflow-executor-catalog.json`
- `repo://codex/bundles/chatgpt-pro-process-workflow-agent-hardening-inputs-v1/inputs/api-captures/workflow-run-e58-events.json`

## Deliverables

- Executor side-effect classification.
- Dry-run/preview/commit contract.
- Idempotency key support for processed markers.
- Duplicate prevention tests.
- Unavailable executor diagnostics.
- Scheduler-safe workflow run guidance/tests.
- Proof manifest and semantic invariants for SB05.

## Dependency Impact

SB08 may use workflow intake scenarios later; SB07 UI must display executor availability and side-effect level; SB09 red-teams duplicate processing.

## Validation Depth

Deep semantic validation for side-effect lifecycle. Must include dry-run negative proof and commit positive proof in a controlled scope.

## Implementation Steps

1. Inventory workflow executors and classify side effects.
2. Add executor availability state and diagnostics.
3. Define dry-run/preview/commit input/output contract.
4. Implement idempotency keys and processed-marker lifecycle for email executors.
5. Ensure retries do not process the same message twice.
6. Add tests for unavailable Gmail executor selection/execution diagnostics.
7. Add tests for Office365 dry-run and controlled commit.
8. Update scheduler guidance if repeated workflows are affected.

## Scope Exceptions

Do not implement a full scheduler refactor unless required for idempotency proof. Do not use real production mailbox categories.

## Do Not Do

- Do not rerun `CanDoItAllSummaryTest` destructively.
- Do not mark a message processed during dry-run.
- Do not hide unavailable executor state.
- Do not treat duplicate message processing as harmless.
- Do not couple Office365-specific behavior into generic workflow engine.

## Acceptance Checklist

- [ ] Executor side-effect classification exists.
- [ ] Dry-run does not mutate external state.
- [ ] Commit mutates only controlled test state.
- [ ] Idempotency prevents duplicate processing.
- [ ] Unavailable executor diagnostics are visible.
- [ ] SB05 proof manifest exists.

## Proof Required


Because this is a critical subbundle, the Semantic Adequacy Gate proof must include:

- `proof/SBxx/manifest.md`
- `proof/SBxx/semantic-invariants.md` or `.json`
- changed-file hashes
- command transcript paths
- source assertions
- shallow-pass trap
- adversarial negative proof
- semantic positive proof
- anti-stub audit
- raw-note literal closure
- dependency smoke proof where stated

Production Behavior Artifact Matrix required for processed-marker records, idempotency records, executor availability state, and external side-effect receipts.


## Browser Validation Logging

Browser validation required if workflow canvas/executor UI is changed. Log route, viewport, executor selection, availability display, side-effect warning, screenshot, console, and result.

## Progression Gate

SB05 passes only when external side effects are safe to test repeatedly and unavailable executors cannot fail unclearly.

## Suggested Agent Prompt

Implement SB05 only. Harden workflow executor side effects and idempotency with controlled tests; never mutate the original evidence category accidentally.
