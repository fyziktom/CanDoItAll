# Processes UI PC2 — correctness and evidence closure

**Executor:** Codex GPT-6 Astra Max  
**Companion:** UI Decoupling Shared Bundle v4.1  
**Reviewed branch:** `components-decoupling`  
**Reviewed commit:** `146067ed133f624878dfe5756c441a43c0f21b4a` (provenance only)

PC1 performed the real extraction. Keep it. This assignment fixes remaining editor/operation correctness, preserves all native runtime and UI behavior, closes source-addressed proof, and delivers a concrete native-authoring prerequisite rather than hiding it in an in-memory workaround.

## Execute

Read [prompt.md](prompt.md), the companion's [signing protocol](../UI_Decoupling_Shared_Bundle/SIGNING_AND_COMMITS.md), [review](REVIEW_PC1.md), [stages](SCOPE_AND_STAGES.md), [state contract](MUTATION_AND_DRAFT_CONTRACT.md), [native owner prerequisite](AUTHORING_OWNER_PREREQUISITE.md), [tests](VALIDATION_MATRIX.md), [browser journeys](BROWSER_JOURNEYS.md), and [closure](CLOSURE_AND_CENSUS.md).

Start by requesting a local PGP unlock. Make verified signed commits throughout coherent tested stages. Work from the operator's current checkout and revalidate drift; never reset to the review SHA.

## Scope in one paragraph

Fix same-opening read/write receipt loss, mutation reentry/submission ownership, authoritative role add/delete selection, and the edited step's hidden decision-role identity. Prove raw drafts and field/row reconciliation, preserve PC1 launch/files/live/chat/voice improvements, and run the selected native/browser/asset/portability checks. Characterize the preexisting authoring persistence gap through the real client lifetime and produce an implementation-ready owner plan with consequences and tests. A new schema, native definition-authority redesign or launch-versioning redesign is **not silently bundled into UI decoupling**; this package describes that boundary precisely in the owner prerequisite. If a current already-approved owner exists on the execution checkout, its bounded adapter repair may be integrated with proof.

Complete all assigned stages in one run where prerequisites permit; a stage checkpoint is not a request for another task. Do not stop after the first defect or replace a missing native proof with a fake success.

## Evidence and tools

The [acceptance plan](acceptance-plan.json) starts unexecuted; copy the [evidence template](evidence-template.json) outside this sealed input. The [PC1 carry-forward table](PC1_CARRY_FORWARD.csv) retains every original group as a feature obligation, without pretending old evidence certifies the current source.

```text
python -B ../UI_Decoupling_Shared_Bundle/tools/handoff_tools.py verify . --shared ../UI_Decoupling_Shared_Bundle
python -B ../UI_Decoupling_Shared_Bundle/tools/handoff_tools.py inspect --repo ../../.. --sources SOURCES.json --output <owned-existing-directory>/pc2-entry.json
```

These are package/source-inspection helpers, not product tests. Review limits and package-only results are in [PACKAGE_VALIDATION.md](PACKAGE_VALIDATION.md). Code, tests, UI strings, documentation, reports and commit messages must be English.
