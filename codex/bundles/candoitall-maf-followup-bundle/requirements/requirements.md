# Requirements

| ID | Requirement | Acceptance criteria |
|---|---|---|
| R01 | Finalizer attachment must use the effective finalizer mode. | Runtime attaches finalizer tools and instructions only when the execution policy resolves to `Required` or `Shadow`; `Disabled` mode attaches no finalizer tool and adds no finalizer prompt text. |
| R02 | Runtime instructions must align with `ResponseFormat`. | Required/shadow finalizer instructions tell the model to call the finalizer and return schema-conformant JSON, not Markdown/prose, when structured response format is configured. |
| R03 | Policy blocks must be distinguished from tool failures. | Tool policy middleware throws/catches a dedicated policy-block exception; downstream tool `InvalidOperationException`/`NotSupportedException` is not mislabeled as a policy block. |
| R04 | Provider capabilities must have one source of truth. | Workspace-backed provider persistence and UI flags use `ProviderProfileService.ResolveFeatureMatrix(...)` or equivalent, not stale transport shortcuts. |
| R05 | Provider transport must be stored explicitly. | The selected provider transport is persisted in provider settings/metadata and read back before falling back to name-based inference. |
| R06 | Verification documentation must match repository contents. | All test classes named in verification docs exist in the ZIP and compile, or the docs are corrected to list the real tests. |
| R07 | Hardening test coverage must be added. | Unit tests exist for finalizer modes, exact-once finalizer validation, runtime finalizer attachment mode alignment, policy-block exception separation, provider matrix consistency, repair extraction, and static markdown/JSON guardrails. |
| R08 | Repair behavior must be truthful and bounded. | The default repair service is documented/tested as conservative JSON extraction, or a separate semantic repair service is implemented with bounded attempts and revalidation. |
| R09 | Process-context validation must be explicit. | Branch outcome, evidence, and governed completion validation are tested as process-context checks, not hidden markdown heuristics. |
| R10 | Unusable mutation tools must fail before model exposure where possible. | Runtime build fails or omits mutation tools when approval is required but no effective approval path can exist for the provider/run. |
| R11 | Workflow/checkpoint claims must be precise. | Docs distinguish pending-approval checkpoint bridging from full MAF Workflow orchestration and document the next adapter step. |
| R12 | Release proof must be reproducible. | Build and focused test commands are recorded with exact results, SDK version, and failures. No fake pass claims. |
