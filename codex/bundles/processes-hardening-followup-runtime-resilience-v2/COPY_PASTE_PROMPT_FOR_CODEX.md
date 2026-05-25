# Copy-Paste Prompt For Codex

You are working in the CanDoItAll repository on the `processes-hardening` branch.

A new follow-up bundle has been added at:

`codex/bundles/processes-hardening-followup-runtime-resilience-v2`

Your task is to execute the bundle one subbundle at a time.

Start by reading:

1. `README.md`
2. `analysis/02-verified-findings.md`
3. `requirements/01-normalized-requirements.md`
4. `architecture/01-target-runtime-architecture.md`
5. `plan/01-phase-plan.md`

Then run the prepared bundle validation. Repair the bundle if it fails.

Implementation priorities:

1. Make process step operation boundaries explicit and generic.
2. Enforce boundaries in tool policy, including external targets and managed output product paths.
3. Fix manager recovery artifact lineage.
4. Add workflow/subprocess artifact adapters and current-run/source-run versioning.
5. Add upstream materialization resolved/unblock lifecycle.
6. Restrict negative disposition routing to appropriate review/approval/decision steps.
7. Make artifact validation storage-backed and explicit-mode capable.
8. Strengthen no-progress retry compression and avoid finalizing active non-terminal executions.
9. Integrate process definition lint into publish/start/readiness.
10. Run generic red-team tests.

Hard constraints:

- Do not reintroduce SQLite.
- Do not make the process core software-development-specific.
- Do not solve scope drift with prompt text only.
- Do not accept source-assertion-only proof.
- Do not let architecture/planning/review steps mutate product targets unless explicitly modeled.
- Do not let artifact-production steps complete through repair/no-go branch routing when their own artifact is missing.

After each subbundle:

- update `proof/SBxx/manifest.md`
- update `proof/SBxx/semantic-invariants.md`
- save transcripts under `proof/SBxx/transcripts/`
- update `reviews/01-execution-report.md`
- run the subbundle gate

After all subbundles:

- run focused integration tests
- run unit tests
- run solution build
- run SQLite residue audit
- update final closure report
