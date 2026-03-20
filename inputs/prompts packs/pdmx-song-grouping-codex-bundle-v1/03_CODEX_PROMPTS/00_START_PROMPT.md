# Start Prompt For Codex

You are implementing the PDMX song-grouping upgrade in the current repository.

Read and follow this bundle in order.

Non-negotiable rules:
- Do not modify the original real indexed DB directly.
- If you need runtime validation on real data, work on a copy/snapshot only.
- Do not keep the current destructive grouping behavior.
- Do not make tags the canonical grouping truth.
- Do not rely on embeddings alone.
- Preserve compatibility with the current workstation routes and core flows.
- Keep code comments in English.

Implementation goal:
- add a production-capable grouping subsystem with:
  - normalization profiles,
  - candidate generation,
  - embeddings,
  - evidence-rich scoring,
  - dry-run proposals,
  - canonical memberships,
  - UI for review and manual correction,
  - tests,
  - validator handoff materials.

Read first:
1. `01_CONTEXT/01_repository_audit.md`
2. `02_DESIGN/01_target_architecture.md`
3. `02_DESIGN/02_data_model.md`
4. `02_DESIGN/03_normalization_strategy.md`
5. `02_DESIGN/05_candidate_generation_and_embeddings.md`

Then execute prompts `01` through `09` in order.

At the end of every prompt:
- summarize what changed,
- list impacted files,
- list remaining risks,
- list tests added or updated,
- state whether any bundle recommendation was intentionally deviated from and why.
