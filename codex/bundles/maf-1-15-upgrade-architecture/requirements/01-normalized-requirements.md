# Normalized Requirements

| ID | Requirement | Verification |
|---|---|---|
| R01 | Capture the 1.13 package graph, runtime behavior, tests, warnings, serialized sessions, pending approvals, and workflow/checkpoint fixtures before package changes. | SB01 proof artifacts exist and are hash-indexed. |
| R02 | Align stable MAF packages to `1.15.0` and A2A packages to `1.15.0-preview.260722.1` without unrelated dependency downgrades. | Restore graph contains one intended MAF release train and no downgrade warnings. |
| R03 | Centralize stable and preview MAF version values without enabling repository-wide Central Package Management as an incidental migration. | Shared MSBuild properties are used by every direct MAF reference. |
| R04 | Preserve live runtime isolation and the immutable preparation/preload architecture. | Concurrency tests prove no cross-run agent, session, tool, provider, MCP, context, or approval leakage. |
| R05 | Keep MAF approval-response binding enabled and prove protection against forged, substituted, replayed, cross-session, and stale approvals. | Security tests pass before closure. |
| R06 | Handle 1.13 sessions with pending approvals explicitly; require native 1.15 serialized binding state and drain or reissue incompatible approvals without private-JSON classification or reconstruction. | Native-state rejection and drain/reissue behavior are tested. |
| R07 | Admit an approval decision only for the complete current server-held pending snapshot, preserving stable request and call IDs and atomic persistence. | Existing model and tests demonstrate snapshot binding, changed-snapshot rejection, and at-most-once consumption. |
| R08 | Remove random fallback generation of approval IDs for persisted/surfaced approval requests. | Missing request IDs fail closed with typed diagnostics. |
| R09 | Preserve 1.13 mixed-tool approval semantics during the initial parity phase, then evaluate 1.15's enabled-by-default bypass in a separate controlled phase. | Explicit option and two-mode test matrix exist. |
| R10 | Return the correct terminal handoff/workflow output on both direct and full runtime paths while retaining useful streaming activity. | Streaming/non-streaming comparison fixtures pass. |
| R11 | Preserve tool-call/tool-result adjacency, message order, reasoning/text order, author names, response IDs, usage, and persisted history. | Response merge regression matrix passes. |
| R12 | Prove 1.13-to-1.15 chat-session and workflow-checkpoint compatibility, including provider-managed conversation IDs and request-scoped attachment scrubbing. | Cross-version fixtures pass or have explicit typed migration outcomes. |
| R13 | Preserve CanDoItAll custom workspace/file tool scope, path safety, external-target authorization, read-only rules, approval wrapping, script policy, and auditing. | File/capability security regression suite passes. |
| R14 | Prove that Harness file access APIs are absent or explicitly configured; do not assume the custom FileTools layer is affected. | Discovery report resolves every Harness/FileAccess match. |
| R15 | Restore, build, and smoke-test A2A hosting on the matching preview package train. | Agent-card, request, stream, session, and error-path evidence exists. |
| R16 | Inventory AG-UI, declarative workflows, compaction, ToolApprovalAgent, Harness, FileMemory, message injection, and OpenAI Responses hosting without adopting them in the compatibility pass. | Optional-feature register has one decision per feature. |
| R17 | Replace blanket experimental-warning suppression with the narrowest justified suppression after a warning inventory. | Warning report and local suppressions are reviewed. |
| R18 | Preserve required-finalizer and structured-output governance unless a specific behavior is proven obsolete after the workflow fixes. | Finalizer characterization and before/after trigger counts exist. |
| R19 | Add structured diagnostics for session serialization/deserialization failures instead of silently swallowing every exception. | Telemetry and tests distinguish timeout, cancellation, malformed state, incompatible state, and provider failure. |
| R20 | Implement a canary rollout and tested rollback strategy that accounts for persisted state written by both 1.13 and 1.15. | Rollout/rollback rehearsal evidence exists. |
| R21 | Keep source-code comments in English and preserve existing codebase architectural conventions. | Review gate passes. |
| R22 | Produce an execution report linking every requirement to source changes, tests, logs, and unresolved exceptions. | Final report has no unowned requirement. |
