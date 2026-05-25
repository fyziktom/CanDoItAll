# Copy/Paste Prompt for Codex

You are working in the `CanDoItAll` repository on the `processes-hardening` branch.

A new follow-up bundle has been added at:

`codex/bundles/processes-hardening-followup-runtime-governance-v5`

Your task is to execute this bundle exactly as an execution-grade CanDoItAll bundle.

Important:
- Do not confuse Processes and Workflows. Workflows are Agent/MAF executors; Processes own the process lifecycle, artifacts, finalization, and governance.
- Keep the process core generic. Do not hardcode Blazor, .NET, software delivery, browser QA, or specific app-development assumptions into the core.
- Do not reintroduce SQLite. Runtime validation and tests must remain PostgreSQL-oriented where database behavior matters.
- Avoid prompt-only fixes. Every subbundle must include production code, tests, and proof.
- Update all proof manifests, semantic invariants, transcripts, and execution report sections.
- Run focused tests first, then broader unit/integration tests, then solution build.
- If a shallow implementation appears sufficient, reject it and add a stronger runtime invariant.

Execute subbundles in this order:

1. `01-persisted-step-operation-contract-schema-ui-import-export`
2. `02-operation-aware-tool-policy-enforcement`
3. `03-trusted-grounding-source-model-and-alias-ledger`
4. `04-storage-service-backed-artifact-validation`
5. `05-artifact-lineage-uniqueness-and-dedup-index`
6. `06-workflow-subprocess-output-contract-adapters`
7. `07-manager-recovery-and-workflow-recovery-continuation`
8. `08-runtime-invariant-audit-and-process-health-dashboard`
9. `09-blocked-failed-escalation-lifecycle`
10. `10-generic-scenario-harness-and-red-team-pack`

Before starting:
- Read `README.md`, `analysis/02-verified-findings.md`, `requirements/01-normalized-requirements.md`, and each subbundle README.
- Run the prepared-bundle validator used by the repository bundle workflow skill.
- Record the command transcript.

After finishing:
- Update `reviews/01-execution-report.md`.
- Add changed-file hashes.
- Run the final closure commands in `scripts/validation-commands.md`.
