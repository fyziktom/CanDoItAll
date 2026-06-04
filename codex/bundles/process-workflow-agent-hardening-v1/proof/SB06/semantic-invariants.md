# SB06 Semantic Invariants

1. Agent role instructions must treat process `allowedOperations` and `operationTargetScope` as canonical contracts, not advisory prose.

2. Product mutations remain permission-gated. Delivery agents must not edit product files when the assigned step lacks `MutateProductTarget`.

3. Browser/runtime proof remains permission-gated. Agents must not use browser proof as completion evidence when the step lacks `CaptureRuntimeProof`.

4. Current-run evidence must stay process-visible. Required proof must include durable receipts such as command output, route, viewport, screenshot, browser state or snapshot, console output, startup identity, cleanup receipt, artifact paths, or lineage fields as applicable.

5. Process templates must name the source catalogs that govern operation contracts. The software-delivery template explicitly ties `allowedOperations` to `ProcessStepOperation` and `operationTargetScope` to `ProcessStepTargetScope`.

6. API skills must reflect current HTTP API behavior instead of removed MCP-only assumptions. Removed MCP mentions are allowed only when they explicitly state the server has been removed or direct users to HTTP API behavior.

7. Provider usage is ledger evidence, not an estimate. Agent/process skills must preserve `ProviderUsageObservationStatus`, `ProviderUsageSourcePhase`, token counts, pricing status, and source execution identifiers.

8. Browser proof is validator-backed. Process guidance must preserve `ProcessBrowserProofValidator` requirements and the structured proof fields it validates.

9. Workflow side effects are catalog contracts. Workflow guidance must preserve `WorkflowExecutorSideEffectDescriptor`, preview/commit separation, `dryRun`, idempotency keys, and external side-effect receipts.

10. Project-structure writeback has an explicit direct-tool boundary. If direct project-structure tools are unavailable, guidance uses the project-structure HTTP API skill rather than reinstalling or assuming a removed MCP server.

11. Active skills must match repository skills before downstream E2E work uses them. Repo and active-root SHA-256 hashes are the source of truth for synchronization.

12. Tests must fail on canonical language drift. The parity tests reject removed canonical descriptor names and stale removed-MCP assumptions, and restored tests pass after those mutations are reverted.
