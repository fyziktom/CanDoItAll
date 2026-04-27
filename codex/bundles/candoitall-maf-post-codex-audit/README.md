# CanDoItAll MAF Post-Codex Audit Bundle

Date: 2026-04-27
Scope: Review of the post-implementation repository snapshot after Codex claimed completion of the previous MAF stabilization bundle.

This bundle is intentionally execution-grade: it is not a design essay. It contains concrete findings, repo evidence, acceptance criteria, and Codex-ready implementation subbundles.

## Execution Status

Status: Implemented and bundle-surface validated on 2026-04-27.

The post-audit gaps R01-R10 have been closed in code and documentation. Focused unit and integration proof is green. Repo-wide integration acceptance remains blocked by unrelated existing failures documented in `docs/agent-runtime-hardening-verification.md`.

## Verdict

Codex implemented several important pieces correctly:

- Structured-output DTOs and top-level object enforcement.
- Runtime `ResponseFormat = ChatResponseFormat.ForJsonSchema(...)` application.
- Continuation paths now preserve structured output contracts.
- Completion-time structured-output validation exists.
- A shadow finalizer tool exists for process-step outcomes.
- MAF function-call middleware/tool policy exists.
- Built-in tool enablement no longer always returns true.
- Calculator-specific recovery guidance was moved out of the generic MAF runtime.
- Unit tests were added for contracts, finalizer policy, tool policy, and provider matrix.

The original audit found these production-stability gaps:

1. Required finalizer mode is not actually enabled for process automation; current process runs pass `MetadataJson: "{}"`, and process-step default is `Shadow`.
2. Assistant chat records are created before required-finalizer validation can replace `runtimeResponse.ResponseText`, so the persisted transcript may not match the final machine output.
3. Output repair/retry is only modeled, not implemented.
4. Provider capability matrix appears inconsistent with Microsoft Agent Framework documentation, especially structured output support and tool approval support.
5. Tool policy returns `RequireApproval`, but middleware only blocks `Deny`/`SkipExecution`; unsupported approval transports can become unsafe if wrappers are absent or misreported.
6. Several validators can throw `NullReferenceException` on missing/null collections instead of returning validation errors.
7. Finalizer support is currently implemented only for `ProcessStepOutcomeResult`, not for the other critical decision DTOs.
8. There was no build/test proof in the audit environment because `dotnet` was unavailable there. Execution proof was captured with .NET SDK 10.0.203.

The implementation closes these with required finalizer policy for governed process runs, transcript persistence after finalization, bounded structured-output repair, provider/approval capability separation, middleware enforcement for ineffective approval paths, null-safe validators, typed finalizers for the critical DTO registry, domain recovery providers, and command proof.

## Contents

- `audit/post-codex-maf-stabilization-audit.md` — primary audit report.
- `audit/evidence-map.md` — file/line evidence from the uploaded repository snapshot.
- `requirements/requirements.md` — normalized requirements R01-R10.
- `subbundles/*` — implementable task bundles for Codex.
- `shared-prompts/codex-master-prompt.md` — prompt to run all subbundles.
- `shared-prompts/codex-qa-prompt.md` — independent verification prompt.
- `reviews/readiness-gate.md` — release readiness gate.
- `scripts/validate_bundle.py` — static bundle structure check.

## External MAF references used by the audit

- Microsoft Agent Framework structured outputs: https://learn.microsoft.com/en-us/agent-framework/agents/structured-outputs
- Microsoft Agent Framework function tools: https://learn.microsoft.com/en-us/agent-framework/agents/tools/function-tools
- Microsoft Agent Framework tool approval: https://learn.microsoft.com/en-us/agent-framework/agents/tools/tool-approval
- Microsoft Agent Framework tools overview/support matrix: https://learn.microsoft.com/en-us/agent-framework/agents/tools/
